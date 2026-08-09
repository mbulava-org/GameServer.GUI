using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using GameServer.Docker.Interfaces;

namespace GameServer.Docker.Services.V2;

/// <summary>
/// Multi-subscriber container attach aggregator.
/// Maintains one agent attach WebSocket per container ID and fans output frames out to
/// all subscribed SignalR clients. The first subscriber to request input becomes the
/// controller; later subscribers are notified who has control and can see output but
/// cannot send input until the controller releases or disconnects.
/// </summary>
public sealed class ContainerAttachAggregator : IContainerAttachAggregator, IAsyncDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ContainerAttachAggregator> _logger;

    // containerId -> shared source
    private readonly ConcurrentDictionary<string, AttachSource> _sources = new();

    public ContainerAttachAggregator(IServiceProvider serviceProvider, ILogger<ContainerAttachAggregator> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AttachStreamFrame> SubscribeAsync(
        string connectionId,
        string containerId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);

        var source = _sources.GetOrAdd(containerId, id => new AttachSource(id, _serviceProvider, _logger));
        var channel = await source.SubscribeAsync(connectionId, cancellationToken).ConfigureAwait(false);

        await foreach (var frame in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return frame;
        }
    }

    /// <inheritdoc />
    public Task<bool> SendInputAsync(
        string connectionId,
        string containerId,
        string input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        if (_sources.TryGetValue(containerId, out var source))
        {
            return source.SendInputAsync(connectionId, input, cancellationToken);
        }

        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task UnsubscribeAsync(string connectionId, string containerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);

        if (_sources.TryGetValue(containerId, out var source))
        {
            source.Unsubscribe(connectionId);
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var source in _sources.Values)
        {
            await source.DisposeAsync().ConfigureAwait(false);
        }

        _sources.Clear();
    }

    /// <summary>
    /// Shared source of attach frames for a single container.
    /// </summary>
    private sealed class AttachSource : IAsyncDisposable
    {
        private readonly string _containerId;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;
        private readonly object _lock = new();
        private readonly List<Subscriber> _subscribers = new();

        private CancellationTokenSource? _cts;
        private Task? _producer;
        private ClientWebSocket? _webSocket;
        private string? _controllerConnectionId;

        public AttachSource(string containerId, IServiceProvider serviceProvider, ILogger logger)
        {
            _containerId = containerId;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public Task<Channel<AttachStreamFrame>> SubscribeAsync(
            string connectionId,
            CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                EnsureStartedLocked();

                var channel = Channel.CreateUnbounded<AttachStreamFrame>(new UnboundedChannelOptions
                {
                    SingleReader = true
                });

                var subscriber = new Subscriber(connectionId, channel);
                channel.Reader.Completion.ContinueWith(_ => Unsubscribe(connectionId), TaskScheduler.Default);
                _subscribers.Add(subscriber);

                if (!string.IsNullOrEmpty(_controllerConnectionId))
                {
                    // Notify the new subscriber who currently has input control.
                    channel.Writer.TryWrite(new AttachStreamFrame
                    {
                        Kind = AttachFrameKind.InputControlledBy,
                        Payload = _controllerConnectionId
                    });
                }

                _logger.LogDebug(
                    "Attach subscriber added for container {ContainerId}. Connection={ConnectionId}, Total={Count}",
                    _containerId, connectionId, _subscribers.Count);

                return Task.FromResult(channel);
            }
        }

        public Task<bool> SendInputAsync(
            string connectionId,
            string input,
            CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                ClientWebSocket? ws = _webSocket;

                if (ws is null || ws.State != WebSocketState.Open)
                    return Task.FromResult(false);

                // First user to send input wins control if unclaimed.
                if (_controllerConnectionId is null)
                {
                    _controllerConnectionId = connectionId;
                    BroadcastControlLocked(_controllerConnectionId);
                }

                if (_controllerConnectionId != connectionId)
                    return Task.FromResult(false);

                var bytes = Encoding.UTF8.GetBytes(input);
                return ws.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken).ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            _logger.LogError(t.Exception, "Failed to send attach input for container {ContainerId}", _containerId);
                        return t.Status == TaskStatus.RanToCompletion;
                    }, TaskScheduler.Default);
            }
        }

        public void Unsubscribe(string connectionId)
        {
            lock (_lock)
            {
                var removed = _subscribers.RemoveAll(s => s.ConnectionId == connectionId);
                if (removed == 0)
                    return;

                _logger.LogDebug(
                    "Attach subscriber removed for container {ContainerId}. Connection={ConnectionId}, Remaining={Count}",
                    _containerId, connectionId, _subscribers.Count);

                if (_controllerConnectionId == connectionId)
                {
                    _controllerConnectionId = null;
                    _logger.LogInformation(
                        "Input controller {ConnectionId} released for container {ContainerId}",
                        connectionId, _containerId);

                    // If anyone else is connected, the next person to send input will win.
                    // Optionally could auto-assign oldest subscriber; input-on-demand is simpler.
                    BroadcastControlLocked(null);
                }

                if (_subscribers.Count == 0)
                {
                    _cts?.Cancel();
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            CancellationTokenSource? cts;
            Task? producer;
            List<Subscriber> subscribers;
            ClientWebSocket? webSocket;

            lock (_lock)
            {
                cts = _cts;
                producer = _producer;
                subscribers = new List<Subscriber>(_subscribers);
                _subscribers.Clear();
                webSocket = _webSocket;
                _webSocket = null;
                _controllerConnectionId = null;
            }

            cts?.Cancel();

            if (webSocket?.State == WebSocketState.Open)
            {
                try
                {
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Aggregator disposing",
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error closing attach WebSocket for container {ContainerId}", _containerId);
                }
            }

            webSocket?.Dispose();
            cts?.Dispose();

            if (producer != null)
            {
                try { await producer.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                catch (Exception ex) { _logger.LogError(ex, "Error stopping attach producer for container {ContainerId}", _containerId); }
            }

            foreach (var subscriber in subscribers)
            {
                subscriber.Channel.Writer.TryComplete();
            }
        }

        private void EnsureStartedLocked()
        {
            if (_producer != null)
                return;

            _cts = new CancellationTokenSource();
            _webSocket = new ClientWebSocket();
            _producer = Task.Run(() => RunProducerAsync(_cts.Token));
        }

        private void BroadcastControlLocked(string? controllerConnectionId)
        {
            var frame = new AttachStreamFrame
            {
                Kind = AttachFrameKind.InputControlledBy,
                Payload = controllerConnectionId ?? string.Empty
            };

            foreach (var subscriber in _subscribers)
            {
                subscriber.Channel.Writer.TryWrite(frame);
            }
        }

        private async Task RunProducerAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var discovery = scope.ServiceProvider.GetRequiredService<INodeAgentDiscovery>();
                var nodeAgentClient = scope.ServiceProvider.GetRequiredService<NodeAgentClient>();

                var agent = await discovery.GetAgentForContainerAsync(_containerId).ConfigureAwait(false);
                if (agent is null)
                {
                    _logger.LogWarning("Cannot attach: no agent for container {ContainerId}", _containerId);
                    BroadcastError("No agent available for container");
                    return;
                }

                // We already created the socket; connect it now.
                ClientWebSocket ws;
                lock (_lock)
                {
                    ws = _webSocket!;
                }

                var wsUrl = BuildAttachWebSocketUrl(agent.InternalUrl, _containerId);
                await ws.ConnectAsync(new Uri(wsUrl), cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(
                    "Attach WebSocket connected for container {ContainerId} via agent {AgentUrl}",
                    _containerId, agent.InternalUrl);

                var buffer = new byte[8192];
                while (ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    var result = await ws.ReceiveAsync(
                        new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        BroadcastOutput(text);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("Agent closed attach stream for container {ContainerId}", _containerId);
                        break;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected shutdown.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Attach producer failed for container {ContainerId}", _containerId);
                BroadcastError($"Attach error: {ex.Message}");
            }
            finally
            {
                List<Subscriber> targets;
                lock (_lock)
                {
                    targets = new List<Subscriber>(_subscribers);
                }

                foreach (var subscriber in targets)
                {
                    subscriber.Channel.Writer.TryComplete();
                }
            }
        }

        private void BroadcastOutput(string text)
        {
            var frame = new AttachStreamFrame
            {
                Kind = AttachFrameKind.Output,
                Payload = text
            };

            List<Subscriber> targets;
            lock (_lock)
            {
                targets = new List<Subscriber>(_subscribers);
            }

            foreach (var subscriber in targets)
            {
                subscriber.Channel.Writer.TryWrite(frame);
            }
        }

        private void BroadcastError(string message)
        {
            var frame = new AttachStreamFrame
            {
                Kind = AttachFrameKind.Error,
                Payload = message
            };

            List<Subscriber> targets;
            lock (_lock)
            {
                targets = new List<Subscriber>(_subscribers);
            }

            foreach (var subscriber in targets)
            {
                subscriber.Channel.Writer.TryWrite(frame);
            }
        }

        private static string BuildAttachWebSocketUrl(string agentUrl, string containerId)
        {
            var baseUrl = agentUrl.TrimEnd('/');
            if (baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                baseUrl = "wss://" + baseUrl.Substring(8);
            else if (baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                baseUrl = "ws://" + baseUrl.Substring(7);
            else if (!baseUrl.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) &&
                     !baseUrl.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
                baseUrl = "ws://" + baseUrl;

            return $"{baseUrl}/containers/{containerId}/attach/ws";
        }

        private sealed class Subscriber
        {
            public string ConnectionId { get; }
            public Channel<AttachStreamFrame> Channel { get; }

            public Subscriber(string connectionId, Channel<AttachStreamFrame> channel)
            {
                ConnectionId = connectionId;
                Channel = channel;
            }
        }
    }
}
