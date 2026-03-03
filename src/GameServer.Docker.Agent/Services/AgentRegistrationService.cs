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
                _nodeName = Environment.GetEnvironmentVariable("NODE_NAME") ?? info.Name ?? Environment.MachineName;

                // Detect if this node is a Swarm manager
                // A node is a manager if Swarm.ControlAvailable is true
                _isManagerNode = info.Swarm?.ControlAvailable ?? false;

                // Determine agent URL
                // In Docker Swarm overlay network, use the task's network IP
                // For now, construct from environment or use hostname
                var agentHost = Environment.GetEnvironmentVariable("AGENT_HOST") ?? _nodeName;
                var agentPort = Environment.GetEnvironmentVariable("AGENT_PORT") ?? "8080";
                _agentUrl = $"http://{agentHost}:{agentPort}";

                _logger.LogInformation(
                    "Agent initialized: NodeId={NodeId}, NodeName={NodeName}, Url={Url}, IsManager={IsManager}",
                    _nodeId,
                    _nodeName,
                    _agentUrl,
                    _isManagerNode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize agent information from Docker daemon");
                throw;
            }
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
            var registration = new
            {
                NodeId = _nodeId,
                NodeName = _nodeName,
                InternalUrl = _agentUrl,
                Capabilities = _options.Capabilities,
                RegisteredAt = DateTime.UtcNow,
                IsManagerNode = _isManagerNode
            };

            await _hubConnection!.InvokeAsync("RegisterAgent", registration);

            _logger.LogInformation(
                "Agent registered with Primary Service: Node={NodeName} ({NodeId}), Capabilities={Capabilities}, Manager={IsManager}",
                _nodeName,
                _nodeId,
                string.Join(", ", _options.Capabilities),
                _isManagerNode);
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
                    _logger.LogWarning("Cannot send heartbeat: SignalR connection is {State}", _hubConnection?.State);
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
            _logger.LogWarning(exception, "Lost connection to Primary Service, reconnecting...");
            return Task.CompletedTask;
        }

        private async Task OnReconnected(string? connectionId)
        {
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
            if (exception != null)
            {
                _logger.LogError(exception, "Connection to Primary Service closed with error");
            }
            else
            {
                _logger.LogInformation("Connection to Primary Service closed gracefully");
            }
            return Task.CompletedTask;
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
