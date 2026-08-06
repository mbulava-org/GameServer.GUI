using Docker.DotNet;
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
        private readonly IDockerClient _dockerClient;
        private readonly ILogger<AgentRegistrationService> _logger;
        private readonly AgentRegistrationOptions _options;
        private HubConnection? _hubConnection;
        private string? _nodeId;
        private string? _nodeName;
        private string? _agentUrl;
        private bool _isManagerNode;
        private bool _primaryServiceShutdownInProgress;

        public AgentRegistrationService(
            IDockerClient dockerClient,
            ILogger<AgentRegistrationService> logger,
            IOptions<AgentRegistrationOptions> options)
        {
            _dockerClient = dockerClient;
            _logger = logger;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("Agent registration is disabled in configuration");
                return;
            }

            if (string.IsNullOrEmpty(_options.PrimaryServiceUrl))
            {
                _logger.LogError("PrimaryServiceUrl is not configured. Agent registration cannot proceed");
                return;
            }

            _logger.LogInformation(
                "Agent Registration Service starting (Primary URL: {PrimaryUrl}, Heartbeat interval: {Interval}s)",
                _options.PrimaryServiceUrl,
                _options.HeartbeatIntervalSeconds);

            // Diagnostic: Log network connectivity details
            try
            {
                var primaryUri = new Uri(_options.PrimaryServiceUrl);
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
            var hubUrl = $"{_options.PrimaryServiceUrl.TrimEnd('/')}/hubs/agentregistration";
            _logger.LogInformation("Connecting to Primary Service at {HubUrl}", hubUrl);

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect(_options.ReconnectDelaySeconds.Select(s => TimeSpan.FromSeconds(s)).ToArray())
                .Build();

            _hubConnection.On<string>("PrimaryServiceShuttingDown", message => HandlePrimaryServiceShutdownAsync(message, stoppingToken));

            // Setup event handlers
            _hubConnection.Reconnecting += OnReconnecting;
            _hubConnection.Reconnected += OnReconnected;
            _hubConnection.Closed += OnClosed;

            // Connect and register with retry logic
            await ConnectAndRegisterWithRetryAsync(stoppingToken);

            // Start heartbeat loop
            await HeartbeatLoopAsync(stoppingToken);
        }

        private async Task ConnectAndRegisterWithRetryAsync(CancellationToken cancellationToken)
        {
            var maxRetries = _options.MaxStartupRetries > 0 ? _options.MaxStartupRetries : 30;
            var currentRetry = 0;
            var baseDelay = TimeSpan.FromSeconds(_options.StartupRetryDelaySeconds > 0 ? _options.StartupRetryDelaySeconds : 5);

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
                // network. Neither the Docker node hostname (e.g. "dev-docker-000", not
                // resolvable across nodes) nor the SignalR connection's remote IP (which in
                // Swarm is often a routing-mesh/load-balancer address, e.g. a service VIP like
                // 10.0.4.6 rather than the task IP 10.0.4.223) are reliable. We inspect this
                // container's own overlay endpoint and prefer a Docker-assigned DNS name (which
                // the embedded DNS resolves regardless of the task IP), falling back to the IP.
                //
                // Resolution order:
                //   1. AGENT_HOST env var (explicit override)
                //   2. This container's DNS name / IP on its shared overlay network
                //   3. Environment.MachineName (task hostname) as a last resort

                var agentPort = Environment.GetEnvironmentVariable("AGENT_PORT") ?? "8080";
                var agentHost = Environment.GetEnvironmentVariable("AGENT_HOST");

                if (string.IsNullOrWhiteSpace(agentHost))
                {
                    agentHost = await ResolveOverlayHostAsync(cancellationToken);
                }

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
        /// Resolves the host (DNS name preferred, IP address as fallback) that the Primary
        /// Service should use to reach this agent on the shared overlay network. Docker node
        /// hostnames are not resolvable across nodes and Swarm routing-mesh source IPs are not
        /// reliable, so we inspect this container's own overlay endpoint. A DNS name is preferred
        /// because Docker's embedded DNS on the overlay resolves it regardless of the task's IP.
        /// </summary>
        private async Task<string?> ResolveOverlayHostAsync(CancellationToken cancellationToken)
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
                    _logger.LogWarning("Agent container '{ContainerId}' has no attached networks to resolve a host from", selfId);
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

                // Prefer a DNS name Docker assigned on this overlay (container name, hostname,
                // short id, network aliases). These are resolvable by the Primary Service via
                // Docker's embedded DNS and are stable even if the task IP changes.
                var dnsName = SelectPreferredDnsName(overlay.Endpoint.DNSNames, overlay.Endpoint.Aliases);

                _logger.LogInformation(
                    "Resolved agent overlay endpoint on network '{Network}': IP={IpAddress}, DNSNames=[{DnsNames}], Aliases=[{Aliases}], Selected={Selected}",
                    overlay.Network,
                    overlay.Endpoint.IPAddress,
                    overlay.Endpoint.DNSNames is null ? string.Empty : string.Join(", ", overlay.Endpoint.DNSNames),
                    overlay.Endpoint.Aliases is null ? string.Empty : string.Join(", ", overlay.Endpoint.Aliases),
                    dnsName ?? overlay.Endpoint.IPAddress);

                // Fall back to the IP address if no DNS name is available.
                return dnsName ?? overlay.Endpoint.IPAddress;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve agent host from the overlay network");
                return null;
            }
        }

        /// <summary>
        /// Chooses the most reachable DNS name for the agent from the names Docker assigned on the
        /// overlay network. Prefers the fully-qualified task name (which contains dots) so it is
        /// unique across replicas, otherwise the first available name.
        /// </summary>
        private static string? SelectPreferredDnsName(IList<string>? dnsNames, IList<string>? aliases)
        {
            var candidates = new List<string>();

            if (dnsNames is not null)
            {
                candidates.AddRange(dnsNames);
            }

            if (aliases is not null)
            {
                candidates.AddRange(aliases);
            }

            var usable = candidates
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (usable.Count == 0)
            {
                return null;
            }

            // Prefer a name containing a dot (e.g. the Swarm task name "svc.1.abc123"), which is
            // unique per task, then fall back to the first available name.
            return usable.FirstOrDefault(n => n.Contains('.')) ?? usable[0];
        }

        private async Task ConnectAndRegisterAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.ConnectionTimeoutSeconds));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                await _hubConnection!.StartAsync(linkedCts.Token);
                _logger.LogInformation("Connected to Primary Service SignalR hub");

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
                _logger.LogError("Connection to Primary Service timed out after {Timeout}s", _options.ConnectionTimeoutSeconds);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Primary Service at {Url}", _options.PrimaryServiceUrl);
                throw;
            }
        }

        private async Task RegisterAsync()
        {
            // Filter capabilities based on node role
            // Only manager nodes can perform service/swarm operations
            var capabilities = FilterCapabilitiesByNodeRole(_options.Capabilities, _isManagerNode);

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
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.HeartbeatIntervalSeconds));

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await SendHeartbeatAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Agent heartbeat loop stopped");
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

                // Get current containers from local Docker
                var containers = await _dockerClient.Containers.ListContainersAsync(
                    new global::Docker.DotNet.Models.ContainersListParameters
                    {
                        All = false // Only running containers
                    },
                    cancellationToken);

                var containerIds = containers.Select(c => c.ID).ToList();

                var heartbeat = new
                {
                    NodeId = _nodeId,
                    ContainerIds = containerIds,
                    Health = "healthy",
                    Timestamp = DateTime.UtcNow
                };

                await _hubConnection.InvokeAsync("SendHeartbeat", heartbeat, cancellationToken);

                _logger.LogTrace(
                    "Heartbeat sent: Node={NodeName}, Containers={ContainerCount}",
                    _nodeName,
                    containerIds.Count);
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

            // Re-register after reconnection
            try
            {
                await RegisterAsync();
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
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, _options.HeartbeatIntervalSeconds)), stoppingToken);
                await ConnectAndRegisterWithRetryAsync(stoppingToken);
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
    }
}
