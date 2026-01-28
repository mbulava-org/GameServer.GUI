using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace GameServer.Web.Services;

/// <summary>
/// Manages WebSocket connections to game servers for logs and console
/// </summary>
public class GameServerWebSocketService : IAsyncDisposable
{
    private readonly ILogger<GameServerWebSocketService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<string, WebSocketConnection> _connections = new();

    public GameServerWebSocketService(
        ILogger<GameServerWebSocketService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Subscribe to log stream for a server
    /// </summary>
    public async Task<IDisposable> SubscribeToLogsAsync(
        string serverId,
        Func<string, Task> onMessageReceived,
        CancellationToken cancellationToken = default)
    {
        var key = $"logs-{serverId}";
        var connection = await GetOrCreateConnectionAsync(key, $"api/servers/{serverId}/ws/logs", cancellationToken);
        
        var subscription = new Subscription(onMessageReceived);
        connection.AddSubscriber(subscription);
        
        return new DisposableAction(() =>
        {
            connection.RemoveSubscriber(subscription);
            if (connection.SubscriberCount == 0)
            {
                _ = CloseConnectionAsync(key);
            }
        });
    }

    /// <summary>
    /// Subscribe to console output for a server
    /// </summary>
    public async Task<IDisposable> SubscribeToConsoleAsync(
        string serverId,
        Func<string, Task> onMessageReceived,
        Func<Task<string>> sendCommandAsync,
        CancellationToken cancellationToken = default)
    {
        var key = $"console-{serverId}";
        var connection = await GetOrCreateConnectionAsync(key, $"api/servers/{serverId}/ws/console", cancellationToken);
        
        var subscription = new Subscription(onMessageReceived, sendCommandAsync);
        connection.AddSubscriber(subscription);
        
        return new DisposableAction(() =>
        {
            connection.RemoveSubscriber(subscription);
            if (connection.SubscriberCount == 0)
            {
                _ = CloseConnectionAsync(key);
            }
        });
    }

    /// <summary>
    /// Send a command to the console
    /// </summary>
    public async Task SendConsoleCommandAsync(
        string serverId,
        string command,
        CancellationToken cancellationToken = default)
    {
        var key = $"console-{serverId}";
        if (!_connections.TryGetValue(key, out var connection))
        {
            throw new InvalidOperationException("Console not connected");
        }

        await connection.SendAsync(command, cancellationToken);
    }

    private async Task<WebSocketConnection> GetOrCreateConnectionAsync(
        string key,
        string relativePath,
        CancellationToken cancellationToken)
    {
        if (_connections.TryGetValue(key, out var existingConnection) && existingConnection.IsConnected)
        {
            return existingConnection;
        }

        var baseUrl = _configuration["GameServerDockerApi:BaseUri"]?.TrimEnd('/') ?? "http://localhost:5164";
        var wsUrl = $"{baseUrl.Replace("http://", "ws://").Replace("https://", "wss://")}/{relativePath}";
        
        var connection = new WebSocketConnection(wsUrl, _logger);

        try
        {
            await connection.ConnectAsync(cancellationToken);
            
            if (_connections.TryAdd(key, connection))
            {
                return connection;
            }
            
            // Another thread created it
            await connection.DisposeAsync();
            if (_connections.TryGetValue(key, out var existingConnection1))
            {
                return existingConnection1;
            }
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        throw new InvalidOperationException("Failed to create connection");
    }

    private async Task CloseConnectionAsync(string key)
    {
        if (_connections.TryRemove(key, out var connection))
        {
            await connection.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        var disposeTasks = _connections.Values.Select(c => c.DisposeAsync().AsTask());
        await Task.WhenAll(disposeTasks);
        _connections.Clear();
    }

    private class WebSocketConnection : IAsyncDisposable
    {
        private readonly string _url;
        private readonly ILogger _logger;
        private readonly List<Subscription> _subscribers = new();
        private readonly SemaphoreSlim _subscriberLock = new(1, 1);
        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _receiveCts;

        public bool IsConnected => _webSocket?.State == WebSocketState.Open;
        public int SubscriberCount => _subscribers.Count;

        public WebSocketConnection(string url, ILogger logger)
        {
            _url = url;
            _logger = logger;
        }

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            _webSocket = new ClientWebSocket();
            _receiveCts = new CancellationTokenSource();

            var uri = new Uri(_url);
            _logger.LogInformation("Connecting to WebSocket: {Url}", _url);
            
            await _webSocket.ConnectAsync(uri, cancellationToken);
            
            _logger.LogInformation("WebSocket connected: {Url}", _url);
            
            // Start receiving loop
            _ = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token));
        }

        public void AddSubscriber(Subscription subscription)
        {
            _subscriberLock.Wait();
            try
            {
                _subscribers.Add(subscription);
            }
            finally
            {
                _subscriberLock.Release();
            }
        }

        public void RemoveSubscriber(Subscription subscription)
        {
            _subscriberLock.Wait();
            try
            {
                _subscribers.Remove(subscription);
            }
            finally
            {
                _subscriberLock.Release();
            }
        }

        public async Task SendAsync(string message, CancellationToken cancellationToken)
        {
            if (_webSocket?.State != WebSocketState.Open)
            {
                throw new InvalidOperationException("WebSocket not connected");
            }

            var bytes = Encoding.UTF8.GetBytes(message);
            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                cancellationToken);
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[1024 * 4];

            try
            {
                while (_webSocket?.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await NotifySubscribersAsync(message);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("WebSocket closed by server: {Url}", _url);
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("WebSocket receive cancelled: {Url}", _url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WebSocket receive error: {Url}", _url);
            }
        }

        private async Task NotifySubscribersAsync(string message)
        {
            await _subscriberLock.WaitAsync();
            List<Subscription> currentSubscribers;
            try
            {
                currentSubscribers = _subscribers.ToList();
            }
            finally
            {
                _subscriberLock.Release();
            }

            var notifyTasks = currentSubscribers.Select(s => 
                Task.Run(async () =>
                {
                    try
                    {
                        await s.OnMessageReceived(message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error notifying subscriber");
                    }
                }));

            await Task.WhenAll(notifyTasks);
        }

        public async ValueTask DisposeAsync()
        {
            _receiveCts?.Cancel();
            _receiveCts?.Dispose();

            if (_webSocket?.State == WebSocketState.Open)
            {
                try
                {
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Disposing",
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error closing WebSocket");
                }
            }

            _webSocket?.Dispose();
            _subscriberLock.Dispose();
        }
    }

    private class Subscription
    {
        public Func<string, Task> OnMessageReceived { get; }
        public Func<Task<string>>? SendCommand { get; }

        public Subscription(Func<string, Task> onMessageReceived, Func<Task<string>>? sendCommand = null)
        {
            OnMessageReceived = onMessageReceived;
            SendCommand = sendCommand;
        }
    }

    private class DisposableAction : IDisposable
    {
        private readonly Action _action;

        public DisposableAction(Action action)
        {
            _action = action;
        }

        public void Dispose()
        {
            _action();
        }
    }
}