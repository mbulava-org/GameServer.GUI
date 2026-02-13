using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GameServer.Docker.Client.Interfaces;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace GameServer.Docker.Client.Services
{
    /// <summary>
    /// SignalR client for real-time container console operations.
    /// Provides bidirectional communication with container consoles through the GameServer.Docker SignalR hub.
    /// </summary>
    public class ContainerConsoleClient : IContainerConsoleClient
    {
        private readonly HubConnection _hubConnection;
        private readonly ILogger<ContainerConsoleClient>? _logger;
        private string? _attachedContainerId;
        private ClientWebSocket? _activeWebSocket; // For direct WebSocket connections (exec)
        private readonly SemaphoreSlim _wsSendLock = new(1, 1); // Thread-safe WebSocket sends

        /// <inheritdoc/>
        public event EventHandler<string>? OutputReceived;

        /// <inheritdoc/>
        public event EventHandler<string>? ErrorReceived;

        /// <inheritdoc/>
        public event EventHandler<string>? Connected;

        /// <inheritdoc/>
        public event EventHandler<string>? Disconnected;

        /// <inheritdoc/>
        public event EventHandler<string>? CommandOutputReceived;

        /// <inheritdoc/>
        public bool IsConnected => _hubConnection.State == HubConnectionState.Connected;

        /// <inheritdoc/>
        public string? AttachedContainerId => _attachedContainerId;

        /// <summary>
        /// Creates a new instance of ContainerConsoleClient
        /// </summary>
        /// <param name="hubUrl">SignalR hub URL (e.g., "https://your-server/hubs/console")</param>
        /// <param name="logger">Optional logger</param>
        public ContainerConsoleClient(string hubUrl, ILogger<ContainerConsoleClient>? logger = null)
        {
            if (string.IsNullOrWhiteSpace(hubUrl))
                throw new ArgumentException("Hub URL cannot be null or empty", nameof(hubUrl));

            _logger = logger;

            // Build SignalR connection
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect(new RetryPolicy())
                .Build();

            // Register event handlers
            RegisterEventHandlers();

            _logger?.LogInformation("ContainerConsoleClient created for hub: {HubUrl}", hubUrl);
        }

        /// <summary>
        /// Creates a new instance using a pre-configured HubConnection
        /// </summary>
        /// <param name="hubConnection">Pre-configured hub connection</param>
        /// <param name="logger">Optional logger</param>
        public ContainerConsoleClient(HubConnection hubConnection, ILogger<ContainerConsoleClient>? logger = null)
        {
            _hubConnection = hubConnection ?? throw new ArgumentNullException(nameof(hubConnection));
            _logger = logger;
            RegisterEventHandlers();
        }

        private void RegisterEventHandlers()
        {
            // Output from container
            _hubConnection.On<string>("Output", (data) =>
            {
                _logger?.LogTrace("Received output: {Length} chars", data.Length);
                OutputReceived?.Invoke(this, data);
            });

            // Error messages
            _hubConnection.On<string>("Error", (message) =>
            {
                _logger?.LogWarning("Received error: {Message}", message);
                ErrorReceived?.Invoke(this, message);
            });

            // Connected to container
            _hubConnection.On<string>("Connected", (containerId) =>
            {
                _logger?.LogInformation("Connected to container: {ContainerId}", containerId);
                _attachedContainerId = containerId;
                Connected?.Invoke(this, containerId);
            });

            // Disconnected from container
            _hubConnection.On<string>("Disconnected", (reason) =>
            {
                _logger?.LogInformation("Disconnected from container: {Reason}", reason);
                _attachedContainerId = null;
                Disconnected?.Invoke(this, reason);
            });

            // Command output
            _hubConnection.On<string>("CommandOutput", (output) =>
            {
                _logger?.LogDebug("Received command output: {Length} chars", output.Length);
                CommandOutputReceived?.Invoke(this, output);
            });

            // Connection state changes
            _hubConnection.Closed += async (error) =>
            {
                _logger?.LogWarning(error, "Hub connection closed");
                _attachedContainerId = null;
                await Task.CompletedTask;
            };

            _hubConnection.Reconnecting += async (error) =>
            {
                _logger?.LogWarning(error, "Hub connection reconnecting");
                await Task.CompletedTask;
            };

            _hubConnection.Reconnected += async (connectionId) =>
            {
                _logger?.LogInformation("Hub connection reconnected: {ConnectionId}", connectionId);
                await Task.CompletedTask;
            };
        }

        /// <inheritdoc/>
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_hubConnection.State == HubConnectionState.Connected)
            {
                _logger?.LogDebug("Already connected to hub");
                return;
            }

            _logger?.LogInformation("Connecting to SignalR hub...");
            await _hubConnection.StartAsync(cancellationToken);
            _logger?.LogInformation("Successfully connected to SignalR hub");
        }

        /// <inheritdoc/>
        public async Task<bool> AttachToContainerAsync(string containerId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(containerId))
                throw new ArgumentException("Container ID cannot be null or empty", nameof(containerId));

            if (_hubConnection.State != HubConnectionState.Connected)
                throw new InvalidOperationException("Not connected to hub. Call ConnectAsync first.");

            _logger?.LogInformation("Attaching to container: {ContainerId}", containerId);

            try
            {
                var result = await _hubConnection.InvokeAsync<bool>(
                    "AttachToContainer",
                    containerId,
                    cancellationToken);

                if (result)
                {
                    _attachedContainerId = containerId;
                    _logger?.LogInformation("Successfully attached to container: {ContainerId}", containerId);
                }
                else
                {
                    _logger?.LogWarning("Failed to attach to container: {ContainerId}", containerId);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error attaching to container: {ContainerId}", containerId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task SendInputAsync(string input, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(input))
                return;

            _logger?.LogTrace("Sending input: {Length} chars", input.Length);

            // If we have an active WebSocket connection (interactive exec), send via WebSocket
            if (_activeWebSocket?.State == WebSocketState.Open)
            {
                await SendToWebSocketAsync(input, cancellationToken);
                return;
            }

            // Otherwise, send via SignalR hub (attach mode)
            if (_hubConnection.State != HubConnectionState.Connected)
                throw new InvalidOperationException("Not connected to hub or WebSocket");

            if (string.IsNullOrEmpty(_attachedContainerId))
                throw new InvalidOperationException("Not attached to any container. Call AttachToContainerAsync or ExecInteractiveAsync first.");

            try
            {
                await _hubConnection.InvokeAsync("SendInput", input, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error sending input via SignalR");
                throw;
            }
        }

        /// <summary>
        /// Send input to WebSocket
        /// </summary>
        private async Task SendToWebSocketAsync(string input, CancellationToken cancellationToken)
        {
            if (_activeWebSocket?.State != WebSocketState.Open)
                return;

            await _wsSendLock.WaitAsync(cancellationToken);
            try
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                await _activeWebSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken);

                _logger?.LogTrace("Sent {ByteCount} bytes to WebSocket", bytes.Length);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error sending to WebSocket");
                throw;
            }
            finally
            {
                _wsSendLock.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<string> ExecCommandAsync(
            string containerId,
            string command,
            string[]? args = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(containerId))
                throw new ArgumentException("Container ID cannot be null or empty", nameof(containerId));

            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentException("Command cannot be null or empty", nameof(command));

            if (_hubConnection.State != HubConnectionState.Connected)
                throw new InvalidOperationException("Not connected to hub");

            _logger?.LogInformation("Executing command in container {ContainerId}: {Command}", containerId, command);

            try
            {
                var result = await _hubConnection.InvokeAsync<string>(
                    "ExecCommand",
                    containerId,
                    command,
                    args ?? Array.Empty<string>(),
                    cancellationToken);

                _logger?.LogDebug("Command executed successfully. Output length: {Length}", result.Length);
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing command in container {ContainerId}", containerId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task ExecInteractiveAsync(
            string agentUrl,
            string containerId,
            string command,
            string[]? args = null,
            bool tty = true,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(agentUrl))
                throw new ArgumentException("Agent URL cannot be null or empty", nameof(agentUrl));

            if (string.IsNullOrWhiteSpace(containerId))
                throw new ArgumentException("Container ID cannot be null or empty", nameof(containerId));

            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentException("Command cannot be null or empty", nameof(command));

            // Build WebSocket URL
            var wsUrl = BuildWebSocketUrl(agentUrl, containerId, command, args, tty);
            _logger?.LogInformation("Starting interactive exec session: {Command} in container {ContainerId}", command, containerId);
            _logger?.LogDebug("WebSocket URL: {Url}", wsUrl);

            using var ws = new ClientWebSocket();

            try
            {
                // Connect to Agent WebSocket endpoint
                await ws.ConnectAsync(new Uri(wsUrl), cancellationToken);
                _logger?.LogInformation("WebSocket connected for interactive exec");

                _activeWebSocket = ws; // Store for SendInputAsync
                _attachedContainerId = containerId;
                Connected?.Invoke(this, containerId);

                // Start bidirectional communication
                var receiveTask = ReceiveFromWebSocketAsync(ws, cancellationToken);
                var sendTask = MonitorInputEventsAsync(ws, cancellationToken);

                // Wait for either task to complete (or cancellation)
                await Task.WhenAny(receiveTask, sendTask);

                _logger?.LogInformation("Interactive exec session ended");
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation("Interactive exec cancelled");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during interactive exec");
                ErrorReceived?.Invoke(this, $"Error during exec: {ex.Message}");
                throw;
            }
            finally
            {
                _activeWebSocket = null; // Clear WebSocket reference
                _attachedContainerId = null;
                Disconnected?.Invoke(this, "Exec session ended");

                if (ws.State == WebSocketState.Open)
                {
                    try
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Session ended", CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Error closing WebSocket");
                    }
                }
            }
        }

        /// <summary>
        /// Build WebSocket URL for interactive exec
        /// </summary>
        private string BuildWebSocketUrl(string agentUrl, string containerId, string command, string[]? args, bool tty)
        {
            // Convert http(s) to ws(s)
            var baseUrl = agentUrl.TrimEnd('/');
            if (baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                baseUrl = "wss://" + baseUrl.Substring(8);
            else if (baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                baseUrl = "ws://" + baseUrl.Substring(7);
            else if (!baseUrl.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) &&
                     !baseUrl.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
                baseUrl = "ws://" + baseUrl;

            // Build query string
            var queryParams = new System.Collections.Generic.List<string>
            {
                $"cmd={Uri.EscapeDataString(command)}"
            };

            if (args != null)
            {
                foreach (var arg in args)
                {
                    queryParams.Add($"cmd={Uri.EscapeDataString(arg)}");
                }
            }

            queryParams.Add($"tty={tty.ToString().ToLowerInvariant()}");

            return $"{baseUrl}/containers/{containerId}/exec/ws?{string.Join("&", queryParams)}";
        }

        /// <summary>
        /// Receive data from WebSocket and raise OutputReceived events
        /// </summary>
        private async Task ReceiveFromWebSocketAsync(ClientWebSocket ws, CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];

            try
            {
                while (ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        _logger?.LogTrace("Received {ByteCount} bytes from WebSocket", result.Count);
                        OutputReceived?.Invoke(this, message);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger?.LogInformation("WebSocket closed by server: {Reason}", result.CloseStatusDescription);
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger?.LogDebug("WebSocket receive cancelled");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error receiving from WebSocket");
                ErrorReceived?.Invoke(this, $"Receive error: {ex.Message}");
            }
        }

        /// <summary>
        /// Monitor for input events and send to WebSocket
        /// This is a placeholder - in practice, you'd send input via SendInputAsync which would need to be adapted for WebSocket
        /// </summary>
        private async Task MonitorInputEventsAsync(ClientWebSocket ws, CancellationToken cancellationToken)
        {
            // This task keeps the connection alive and allows SendInputAsync to work
            // In a real implementation, you might use a Channel or similar to queue input
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger?.LogDebug("Input monitor cancelled");
            }
        }

        /// <inheritdoc/>
        public async Task DisconnectFromContainerAsync(CancellationToken cancellationToken = default)
        {
            if (_hubConnection.State != HubConnectionState.Connected)
                return;

            if (string.IsNullOrEmpty(_attachedContainerId))
                return;

            _logger?.LogInformation("Disconnecting from container: {ContainerId}", _attachedContainerId);

            try
            {
                await _hubConnection.InvokeAsync("Disconnect", cancellationToken);
                _attachedContainerId = null;
                _logger?.LogInformation("Successfully disconnected from container");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disconnecting from container");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (_hubConnection.State == HubConnectionState.Disconnected)
                return;

            _logger?.LogInformation("Stopping SignalR connection");

            try
            {
                await _hubConnection.StopAsync(cancellationToken);
                _attachedContainerId = null;
                _logger?.LogInformation("SignalR connection stopped");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error stopping SignalR connection");
                throw;
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            try
            {
                // Close active WebSocket if any
                if (_activeWebSocket?.State == WebSocketState.Open)
                {
                    await _activeWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disposing", CancellationToken.None);
                }
                _activeWebSocket?.Dispose();

                await DisconnectFromContainerAsync();
                await StopAsync();
                await _hubConnection.DisposeAsync();
                
                _wsSendLock.Dispose();
                
                _logger?.LogInformation("ContainerConsoleClient disposed");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing ContainerConsoleClient");
            }
        }

        /// <summary>
        /// Custom retry policy for SignalR reconnection
        /// </summary>
        private class RetryPolicy : IRetryPolicy
        {
            public TimeSpan? NextRetryDelay(RetryContext retryContext)
            {
                // Exponential backoff: 0s, 2s, 10s, 30s, then stop
                return retryContext.PreviousRetryCount switch
                {
                    0 => TimeSpan.Zero,
                    1 => TimeSpan.FromSeconds(2),
                    2 => TimeSpan.FromSeconds(10),
                    3 => TimeSpan.FromSeconds(30),
                    _ => null // Stop retrying
                };
            }
        }
    }
}
