using Docker.DotNet;
using Docker.DotNet.Models;
using GameServer.Docker.Agent.Configurations;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace GameServer.Docker.Agent.Services
{
    /// <summary>
    /// Background service that registers with the Primary Service and sends periodic heartbeats.
    /// This is the new architecture where agents push their state instead of being discovered.
    /// </summary>
    public class AgentRegistrationService : BackgroundService
    {
        private const string ManagedLabelKey = "gameserver.docker.managed";
        private const string ManagedLabelValue = "true";
        private const string ServerIdLabelKey = "gameserver.docker.Id";

        private readonly IDockerClient _dockerClient;
        private readonly AgentDistributedConfigurationApplier _distributedConfigurationApplier;
        private readonly ILogger<AgentRegistrationService> _logger;
        private readonly IOptionsMonitor<AgentRegistrationOptions> _optionsMonitor;
        private HubConnection? _hubConnection;
        private string? _nodeId;
        private string? _nodeName;
        private string? _agentUrl;
        private bool _isManagerNode;
        private bool _primaryServiceShutdownInProgress;

        public AgentRegistrationService(
            IDockerClient dockerClient,
            AgentDistributedConfigurationApplier distributedConfigurationApplier,
            ILogger<AgentRegistrationService> logger,
            IOptionsMonitor<AgentRegistrationOptions> optionsMonitor)
        {
            _dockerClient = dockerClient;
            _distributedConfigurationApplier = distributedConfigurationApplier;
            _logger = logger;
            _optionsMonitor = optionsMonitor;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var bootstrapOptions = _optionsMonitor.CurrentValue;

            if (!bootstrapOptions.Enabled)
            {
                _logger.LogInformation("Agent registration is disabled in configuration");
                return;
            }

            if (string.IsNullOrEmpty(bootstrapOptions.PrimaryServiceUrl))
            {
                _logger.LogError("PrimaryServiceUrl is not configured. Agent registration cannot proceed");
                return;
            }

            _logger.LogInformation(
                "Agent Registration Service starting (Primary URL: {PrimaryUrl}, Heartbeat interval: {Interval}s)",
                bootstrapOptions.PrimaryServiceUrl,
                bootstrapOptions.HeartbeatIntervalSeconds);

            // Diagnostic: Log network connectivity details
            try
            {
                var primaryUri = new Uri(bootstrapOptions.PrimaryServiceUrl);
                _logger.LogDebug(
                    "Primary Service connectivity check: Host={Host}, Port={Port}, Scheme={Scheme}",
                    primaryUri.Host,
                    primaryUri.Port,
                    primaryUri.Scheme);

                // Log environment info
                _logger.LogDebug(
                    "Agent environment: Hostname={Hostname}, Machine={Machine}",
                    Environment.GetEnvironmentVariable("HOSTNAME") ?? "not set",
                    Environment.MachineName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse Primary Service URL for diagnostics");
            }

            // Initialize agent information
            await InitializeAgentInfoAsync(stoppingToken);

            if (string.IsNullOrEmpty(_nodeId))
            {
                _logger.LogError("Could not determine node ID. Agent registration cannot proceed");
                return;
            }

            // Build SignalR connection to Primary Service
            var hubUrl = $"{bootstrapOptions.PrimaryServiceUrl.TrimEnd('/')}/hubs/agentregistration";
            _logger.LogInformation("Connecting to Primary Service at {HubUrl}", hubUrl);

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect(bootstrapOptions.ReconnectDelaySeconds.Select(s => TimeSpan.FromSeconds(s)).ToArray())
                .Build();

            _hubConnection.On<string>("PrimaryServiceShuttingDown", message => HandlePrimaryServiceShutdownAsync(message, stoppingToken));

            // Setup event handlers
            _hubConnection.Reconnecting += OnReconnecting;
            _hubConnection.Reconnected += OnReconnected;
            _hubConnection.Closed += OnClosed;

            // Connect and register with retry logic
            await ConnectAndRegisterWithRetryAsync(stoppingToken);

            await PublishManagedContainerSnapshotAsync(stoppingToken);

            // Start heartbeat and reconciliation loops
            await Task.WhenAll(
                HeartbeatLoopAsync(stoppingToken),
                ManagedContainerReconciliationLoopAsync(stoppingToken));
        }

        private async Task ConnectAndRegisterWithRetryAsync(CancellationToken cancellationToken)
        {
            var bootstrapOptions = _optionsMonitor.CurrentValue;
            var maxRetries = bootstrapOptions.MaxStartupRetries > 0 ? bootstrapOptions.MaxStartupRetries : 30;
            var currentRetry = 0;
            var baseDelay = TimeSpan.FromSeconds(bootstrapOptions.StartupRetryDelaySeconds > 0 ? bootstrapOptions.StartupRetryDelaySeconds : 5);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await ConnectAndRegisterAsync(cancellationToken);
                    _logger.LogInformation("Successfully connected and registered with Primary Service");
                    return; // Success!
                }
                catch (Exception ex) when (currentRetry < maxRetries)
                {
                    currentRetry++;
                    var delay = TimeSpan.FromSeconds(Math.Min(baseDelay.TotalSeconds * Math.Pow(1.5, currentRetry - 1), 60));

                    if(currentRetry % 5 == 0) // Log every 5 attempts
                        _logger.LogWarning(
                            ex,
                            "Failed to connect to Primary Service (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}s...",
                            currentRetry,
                            maxRetries,
                            delay.TotalSeconds);

                    try
                    {
                        await Task.Delay(delay, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Agent startup cancelled during retry delay");
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to connect to Primary Service after {Attempts} attempts. Giving up.",
                        currentRetry);
                    throw;
                }
            }

            throw new OperationCanceledException("Agent startup cancelled before successful connection");
        }

        private async Task InitializeAgentInfoAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Get node information from local Docker daemon
                var info = await _dockerClient.System.GetSystemInfoAsync(cancellationToken);

                _nodeId = info.Swarm?.NodeID ?? Guid.NewGuid().ToString();

                // For node name, use the Docker node's hostname
                // This identifies which physical/VM node the agent is running on
                _nodeName = Environment.GetEnvironmentVariable("NODE_NAME") ?? info.Name ?? Environment.MachineName;

                // Detect if this node is a Swarm manager
                _isManagerNode = info.Swarm?.ControlAvailable ?? false;

                // ===== AGENT URL CONFIGURATION =====
                // CRITICAL: The Primary Service reaches the agent across the shared overlay
                // network. The Docker node hostname (e.g. "dev-docker-000") is not resolvable
                // across nodes, and the SignalR connection's remote IP in Swarm is often a
                // routing-mesh/load-balancer address (e.g. a service VIP like 10.0.4.6 rather
                // than the task IP 10.0.4.223). To be deterministic, the agent always registers
                // with its own current overlay task IP address, which is the correct/current
                // address for this replica.
                //
                // Resolution order:
                //   1. This container's IP on its shared overlay network (preferred)
                //   2. Environment.MachineName (task hostname) as a last resort

                var agentPort = Environment.GetEnvironmentVariable("AGENT_PORT") ?? "8080";
                var agentHost = await ResolveOverlayIpAsync(cancellationToken);

                if (string.IsNullOrWhiteSpace(agentHost))
                {
                    _logger.LogWarning(
                        "Could not resolve an overlay network IP for the agent. Falling back to hostname '{Hostname}', " +
                        "which may not be reachable from the Primary Service across nodes.",
                        Environment.MachineName);
                    agentHost = Environment.MachineName;
                }

                _agentUrl = $"http://{agentHost}:{agentPort}";

                _logger.LogInformation(
                    "🌐 Agent overlay IP address resolved: {IpAddress} (AgentUrl={Url})",
                    agentHost,
                    _agentUrl);

                _logger.LogInformation(
                    "Agent initialized: NodeId={NodeId}, NodeName={NodeName}, AgentUrl={Url}, IsManager={IsManager}",
                    _nodeId,
                    _nodeName,
                    _agentUrl,
                    _isManagerNode);

                _logger.LogDebug(
                    "Agent network identity: DockerNodeHostname={DockerNode}, TaskHostname={TaskHostname}, ServiceUrl={ServiceUrl}",
                    info.Name,
                    Environment.MachineName,
                    _agentUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize agent information from Docker daemon");
                throw;
            }
        }

        // Docker-managed infrastructure networks that are never the shared overlay used to
        // reach the agent. Any other attached network exposing an IP is the shared overlay.
        private static readonly HashSet<string> InfrastructureNetworks = new(StringComparer.OrdinalIgnoreCase)
        {
            "ingress",
            "bridge",
            "host",
            "none",
            "docker_gwbridge"
        };

        /// <summary>
        /// Resolves the current overlay task IP address that the Primary Service should use to
        /// reach this agent on the shared overlay network. Docker node hostnames are not
        /// resolvable across nodes and Swarm routing-mesh source IPs are not reliable, so we
        /// inspect this container's own overlay endpoint and use its assigned IP address.
        /// </summary>
        private async Task<string?> ResolveOverlayIpAsync(CancellationToken cancellationToken)
        {
            try
            {
                // The container hostname (HOSTNAME / Environment.MachineName) is the container ID
                // in Docker, which Docker inspect accepts.
                var selfId = Environment.GetEnvironmentVariable("HOSTNAME");
                if (string.IsNullOrWhiteSpace(selfId))
                {
                    selfId = Environment.MachineName;
                }

                var container = await _dockerClient.Containers.InspectContainerAsync(selfId, cancellationToken);

                var networks = container?.NetworkSettings?.Networks;
                if (networks is null || networks.Count == 0)
                {
                    _logger.LogWarning("Agent container '{ContainerId}' has no attached networks to resolve an IP from", selfId);
                    return null;
                }

                // Pick the first attached network that exposes a usable IP and is not a Docker
                // infrastructure network (ingress, bridge, host, none, docker_gwbridge). This is
                // the shared overlay network the Primary Service and game services live on.
                var overlay = networks
                    .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value?.IPAddress)
                        && !InfrastructureNetworks.Contains(kvp.Key))
                    .Select(kvp => new { Network = kvp.Key, Endpoint = kvp.Value! })
                    .FirstOrDefault();

                if (overlay is null)
                {
                    _logger.LogWarning(
                        "Could not find a non-infrastructure overlay network on agent container '{ContainerId}'. " +
                        "Attached networks: {Networks}",
                        selfId,
                        string.Join(", ", networks.Keys));

                    return null;
                }

                _logger.LogInformation(
                    "Resolved agent overlay endpoint on network '{Network}': IP={IpAddress}",
                    overlay.Network,
                    overlay.Endpoint.IPAddress);

                // Always register with the current overlay task IP address, which is the
                // correct/current address for this replica.
                return overlay.Endpoint.IPAddress;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve agent IP from the overlay network");
                return null;
            }
        }

        private async Task ConnectAndRegisterAsync(CancellationToken cancellationToken)
        {
            try
            {
                var currentOptions = _optionsMonitor.CurrentValue;
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(currentOptions.ConnectionTimeoutSeconds));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                await _hubConnection!.StartAsync(linkedCts.Token);
                _logger.LogInformation("Connected to Primary Service SignalR hub");

                await SyncDistributedConfigurationAsync(linkedCts.Token);

                // Send initial registration
                await RegisterAsync();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Agent registration cancelled");
                throw;
            }
            catch (OperationCanceledException)
            {
                _logger.LogError("Connection to Primary Service timed out after {Timeout}s", _optionsMonitor.CurrentValue.ConnectionTimeoutSeconds);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Primary Service at {Url}", _optionsMonitor.CurrentValue.PrimaryServiceUrl);
                throw;
            }
        }

        private async Task RegisterAsync()
        {
            var currentOptions = _optionsMonitor.CurrentValue;

            // Filter capabilities based on node role
            // Only manager nodes can perform service/swarm operations
            var capabilities = FilterCapabilitiesByNodeRole(currentOptions.Capabilities, _isManagerNode);

            var registration = new
            {
                NodeId = _nodeId,
                NodeName = _nodeName,
                InternalUrl = _agentUrl,
                Capabilities = capabilities,
                RegisteredAt = DateTime.UtcNow,
                IsManagerNode = _isManagerNode
            };

            await _hubConnection!.InvokeAsync("RegisterAgent", registration);

            _logger.LogInformation(
                "Agent registered with Primary Service: Node={NodeName} ({NodeId}), Capabilities={Capabilities}, Manager={IsManager}",
                _nodeName,
                _nodeId,
                string.Join(", ", capabilities),
                _isManagerNode);
        }

        private async Task SyncDistributedConfigurationAsync(CancellationToken cancellationToken)
        {
            var settings = await _hubConnection!
                .InvokeAsync<Dictionary<string, string?>?>("GetDistributedConfiguration", cancellationToken);

            _distributedConfigurationApplier.Apply(settings ?? new Dictionary<string, string?>());
        }

        private static List<string> FilterCapabilitiesByNodeRole(List<string> configuredCapabilities, bool isManagerNode)
        {
            // Capabilities that require manager node access to Docker Swarm API
            // These operations use Docker.DotNet endpoints that are only available on manager nodes:
            // - ISwarmOperations (services, tasks, nodes)
            // - Service management (create, update, delete)
            var managerOnlyCapabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "services",  // Service operations: IDockerClient.Swarm.* (requires manager)
                "tasks",     // Task operations: IDockerClient.Tasks.* (requires manager)
                "nodes",     // Node operations: IDockerClient.Swarm.* nodes (requires manager)
                "swarm"      // Swarm operations: IDockerClient.Swarm.* config (requires manager)
            };

            // Worker nodes can only perform container-level operations
            // These use IContainerOperations which works on any node
            if (!isManagerNode)
            {
                var filtered = configuredCapabilities
                    .Where(cap => !managerOnlyCapabilities.Contains(cap))
                    .ToList();

                return filtered.Distinct().ToList();
            }

            // Manager nodes get all configured capabilities
            return configuredCapabilities.Distinct().ToList();
        }

        private async Task HeartbeatLoopAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var heartbeatIntervalSeconds = Math.Max(1, _optionsMonitor.CurrentValue.HeartbeatIntervalSeconds);
                    await Task.Delay(TimeSpan.FromSeconds(heartbeatIntervalSeconds), stoppingToken);
                    await SendHeartbeatAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Agent heartbeat loop stopped");
            }
        }

        private async Task ManagedContainerReconciliationLoopAsync(CancellationToken stoppingToken)
        {
            var intervalSeconds = Math.Clamp(_options.ManagedContainerReconciliationIntervalSeconds, 5, 300);
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await PublishManagedContainerSnapshotAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Managed container reconciliation loop stopped");
            }
        }

        private async Task SendHeartbeatAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (_hubConnection?.State != HubConnectionState.Connected)
                {
                    if (_primaryServiceShutdownInProgress)
                    {
                        _logger.LogDebug("Skipping heartbeat while waiting for Primary Service shutdown/restart.");
                    }
                    else
                    {
                        _logger.LogWarning("Cannot send heartbeat: SignalR connection is {State}", _hubConnection?.State);
                    }

                    return;
                }

                var heartbeat = new
                {
                    NodeId = _nodeId,
                    Health = "healthy",
                    Timestamp = DateTime.UtcNow
                };

                await _hubConnection.InvokeAsync("SendHeartbeat", heartbeat, cancellationToken);

                _logger.LogTrace(
                    "Heartbeat sent: Node={NodeName}",
                    _nodeName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send heartbeat to Primary Service");
            }
        }

        private Task OnReconnecting(Exception? exception)
        {
            if (_primaryServiceShutdownInProgress)
            {
                _logger.LogInformation("Primary Service shutdown in progress. Pausing until reconnect loop resumes.");
                return Task.CompletedTask;
            }

            if (IsUnexpectedDisconnect(exception))
            {
                _logger.LogWarning("Primary Service disconnected unexpectedly. The agent will reconnect automatically.");
                _logger.LogDebug(exception, "Detailed disconnect information");
            }
            else
            {
                _logger.LogInformation("Connection to Primary Service closed. Reconnecting automatically.");
            }

            return Task.CompletedTask;
        }

        private static bool IsUnexpectedDisconnect(Exception? exception)
        {
            if (exception is null)
            {
                return false;
            }

            var message = exception.Message;
            return message.Contains("closed", StringComparison.OrdinalIgnoreCase)
                || message.Contains("reset by peer", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Connection reset", StringComparison.OrdinalIgnoreCase)
                || message.Contains("close handshake", StringComparison.OrdinalIgnoreCase)
                || exception is System.Net.WebSockets.WebSocketException;
        }

        private async Task OnReconnected(string? connectionId)
        {
            _primaryServiceShutdownInProgress = false;
            _logger.LogInformation("Reconnected to Primary Service with ConnectionId={ConnectionId}", connectionId);

            // Refresh distributed configuration and re-register after reconnection
            try
            {
                await SyncDistributedConfigurationAsync(CancellationToken.None);
                await RegisterAsync();
                await PublishManagedContainerSnapshotAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to re-register after reconnection");
            }

            return;
        }

        private Task OnClosed(Exception? exception)
        {
            if (_primaryServiceShutdownInProgress)
            {
                _logger.LogInformation("Connection to Primary Service closed during coordinated shutdown.");
                return Task.CompletedTask;
            }

            if (exception != null)
            {
                _logger.LogWarning("Connection to Primary Service closed unexpectedly. The agent will reconnect automatically.");
                _logger.LogDebug(exception, "Detailed close error information");
            }
            else
            {
                _logger.LogInformation("Connection to Primary Service closed gracefully");
            }
            return Task.CompletedTask;
        }

        private async Task HandlePrimaryServiceShutdownAsync(string? message, CancellationToken stoppingToken)
        {
            if (stoppingToken.IsCancellationRequested || _hubConnection == null || _primaryServiceShutdownInProgress)
            {
                return;
            }

            _primaryServiceShutdownInProgress = true;
            _logger.LogInformation("Primary Service signaled shutdown: {Message}", string.IsNullOrWhiteSpace(message) ? "No reason provided." : message);

            try
            {
                await _hubConnection.StopAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping SignalR connection after Primary Service shutdown signal.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, _optionsMonitor.CurrentValue.HeartbeatIntervalSeconds)), stoppingToken);
                await ConnectAndRegisterWithRetryAsync(stoppingToken);
                await PublishManagedContainerSnapshotAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Primary Service graceful reconnect loop exited with an error.");
            }
            finally
            {
                _primaryServiceShutdownInProgress = false;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Agent Registration Service");

            if (_hubConnection != null)
            {
                try
                {
                    await _hubConnection.StopAsync(cancellationToken);
                    await _hubConnection.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping SignalR connection");
                }
            }

            await base.StopAsync(cancellationToken);
        }

        private async Task PublishManagedContainerSnapshotAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (_hubConnection?.State != HubConnectionState.Connected || string.IsNullOrWhiteSpace(_nodeId))
                {
                    return;
                }

                var filterValue = $"{ManagedLabelKey}={ManagedLabelValue}";
                var containers = await _dockerClient.Containers.ListContainersAsync(
                    new ContainersListParameters
                    {
                        All = false,
                        Filters = new Dictionary<string, IDictionary<string, bool>>
                        {
                            ["label"] = new Dictionary<string, bool>
                            {
                                [filterValue] = true
                            }
                        }
                    },
                    cancellationToken);

                var managedContainers = containers
                    .Select(c => new
                    {
                        ContainerId = c.ID ?? string.Empty,
                        Labels = c.Labels ?? new Dictionary<string, string>(StringComparer.Ordinal)
                    })
                    .Where(c =>
                        !string.IsNullOrWhiteSpace(c.ContainerId) &&
                        c.Labels.TryGetValue(ManagedLabelKey, out var managedValue) &&
                        string.Equals(managedValue, ManagedLabelValue, StringComparison.OrdinalIgnoreCase) &&
                        c.Labels.TryGetValue(ServerIdLabelKey, out var serverId) &&
                        !string.IsNullOrWhiteSpace(serverId))
                    .Select(c => new
                    {
                        ContainerId = c.ContainerId,
                        ServerId = c.Labels[ServerIdLabelKey],
                        ManagedLabelValue = c.Labels[ManagedLabelKey]
                    })
                    .ToList();

                var payload = new
                {
                    NodeId = _nodeId,
                    Containers = managedContainers,
                    Timestamp = DateTime.UtcNow
                };

                await _hubConnection.InvokeAsync("UpdateManagedContainers", payload, cancellationToken);

                _logger.LogTrace(
                    "Published managed-container snapshot for node {NodeId}. Accepted containers: {Count}",
                    _nodeId,
                    managedContainers.Count);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish managed-container snapshot");
            }
        }
    }
}
