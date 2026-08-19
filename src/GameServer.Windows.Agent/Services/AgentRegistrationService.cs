using GameServer.Windows.Agent.Configurations;
using GameServer.Windows.Agent.Interfaces;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace GameServer.Windows.Agent.Services;

public class AgentRegistrationService : BackgroundService
{
    private readonly ILogger<AgentRegistrationService> _logger;
    private readonly AgentRegistrationOptions _options;
    private readonly WindowsAgentOptions _rootOptions;
    private readonly IGameProcessManager _processManager;
    private HubConnection? _hubConnection;
    private string _nodeId = string.Empty;
    private string _nodeName = string.Empty;
    private string _agentUrl = string.Empty;

    public AgentRegistrationService(
        ILogger<AgentRegistrationService> logger,
        IOptions<WindowsAgentOptions> options,
        IGameProcessManager processManager)
    {
        _logger = logger;
        _rootOptions = options.Value;
        _options = options.Value.AgentRegistration;
        _processManager = processManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Agent registration is disabled in configuration.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.PrimaryServiceUrl))
        {
            _logger.LogError("PrimaryServiceUrl is not configured. Agent registration cannot proceed.");
            return;
        }

        InitializeIdentity();

        var hubUrl = $"{_options.PrimaryServiceUrl.TrimEnd('/')}/hubs/agentregistration";
        _logger.LogInformation("Windows Agent connecting to Primary Service at {HubUrl}", hubUrl);

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect(_options.ReconnectDelaySeconds.Select(s => TimeSpan.FromSeconds(s)).ToArray())
            .Build();

        _hubConnection.Reconnecting += ex =>
        {
            _logger.LogWarning(ex, "SignalR connection to Primary Service dropped. Reconnecting...");
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += async connectionId =>
        {
            _logger.LogInformation("Reconnected to Primary Service with ConnectionId={ConnectionId}", connectionId);
            try
            {
                await RegisterAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to re-register after reconnection");
            }
        };

        _hubConnection.Closed += ex =>
        {
            if (ex != null)
            {
                _logger.LogWarning(ex, "Connection to Primary Service closed unexpectedly");
            }
            return Task.CompletedTask;
        };

        await ConnectAndRegisterWithRetryAsync(stoppingToken).ConfigureAwait(false);
        await HeartbeatLoopAsync(stoppingToken).ConfigureAwait(false);
    }

    private void InitializeIdentity()
    {
        _nodeId = !string.IsNullOrWhiteSpace(_options.NodeId)
            ? _options.NodeId
            : $"win-{Environment.MachineName.ToLowerInvariant()}";

        _nodeName = !string.IsNullOrWhiteSpace(_options.NodeName)
            ? _options.NodeName
            : Environment.MachineName;

        var hostIp = GetLocalIpAddress();
        _agentUrl = $"http://{hostIp}:{_rootOptions.AgentPort}";

        _logger.LogInformation("Windows Agent identity initialized: NodeId={NodeId}, NodeName={NodeName}, AgentUrl={AgentUrl}",
            _nodeId, _nodeName, _agentUrl);
    }

    private static string GetLocalIpAddress()
    {
        try
        {
            using var socket = new System.Net.Sockets.Socket(
                System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is System.Net.IPEndPoint endPoint)
            {
                return endPoint.Address.ToString();
            }
        }
        catch
        {
            // Fallback
        }

        return Environment.MachineName;
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
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.ConnectionTimeoutSeconds));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                await _hubConnection!.StartAsync(linkedCts.Token).ConfigureAwait(false);
                _logger.LogInformation("Connected to Primary Service SignalR hub");

                await RegisterAsync().ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (currentRetry < maxRetries && !cancellationToken.IsCancellationRequested)
            {
                currentRetry++;
                var delay = TimeSpan.FromSeconds(Math.Min(baseDelay.TotalSeconds * Math.Pow(1.5, currentRetry - 1), 60));

                if (currentRetry % 5 == 0 || currentRetry == 1)
                {
                    _logger.LogWarning(ex, "Failed to connect to Primary Service (attempt {Attempt}/{Max}). Retrying in {Delay}s...",
                        currentRetry, maxRetries, delay.TotalSeconds);
                }

                try
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
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
            IsManagerNode = false,
            HostType = "windows"
        };

        await _hubConnection!.InvokeAsync("RegisterAgent", registration).ConfigureAwait(false);

        _logger.LogInformation("Registered Windows Agent with Primary Service: Node={NodeName} ({NodeId})",
            _nodeName, _nodeId);
    }

    private async Task HeartbeatLoopAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.HeartbeatIntervalSeconds));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                if (_hubConnection?.State == HubConnectionState.Connected)
                {
                    var runningServers = _processManager.GetAllServers()
                        .Where(s => s.Status == Models.ServerProcessStatus.Running)
                        .Select(s => s.ServerId)
                        .ToList();

                    var heartbeat = new
                    {
                        NodeId = _nodeId,
                        ContainerIds = runningServers, // Reuses containerIds property for compatibility with existing hub
                        Health = "healthy",
                        Timestamp = DateTime.UtcNow
                    };

                    await _hubConnection.InvokeAsync("SendHeartbeat", heartbeat, stoppingToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Windows Agent heartbeat loop");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Windows Agent Registration Service");

        if (_hubConnection != null)
        {
            try
            {
                await _hubConnection.StopAsync(cancellationToken).ConfigureAwait(false);
                await _hubConnection.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping SignalR registration connection");
            }
        }

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}
