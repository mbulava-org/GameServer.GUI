using System;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GameServer.Docker.Client.Interfaces;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace GameServer.Docker.Client.Services
{
    /// <summary>
    /// SignalR client for a shared, multi-subscriber container attach stream.
    /// Targets the /hubs/attach endpoint. Output is fanned out to all viewers;
    /// input is accepted only from the current controller.
    /// </summary>
    public class ContainerConsoleClient : IContainerConsoleClient
    {
        private readonly HubConnection _hubConnection;
        private readonly ILogger<ContainerConsoleClient>? _logger;
        private string? _attachedContainerId;
        private CancellationTokenSource? _streamCts;

        /// <inheritdoc/>
        public event EventHandler<string>? OutputReceived;

        /// <inheritdoc/>
        public event EventHandler<string>? ErrorReceived;

        /// <inheritdoc/>
        public event EventHandler<string>? Connected;

        /// <inheritdoc/>
        public event EventHandler<string>? Disconnected;

        /// <inheritdoc/>
        public event EventHandler<string>? InputControlChanged;

        /// <inheritdoc/>
        public bool IsConnected => _hubConnection.State == HubConnectionState.Connected;

        /// <inheritdoc/>
        public string? AttachedContainerId => _attachedContainerId;

        /// <inheritdoc/>
        public string? ConnectionId => _hubConnection.ConnectionId;

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

        private void ProcessMessage(string message)
        {
            try
            {
                var frame = JsonSerializer.Deserialize<AttachStreamMessage>(message);
                if (frame is null)
                {
                    OutputReceived?.Invoke(this, message);
                    return;
                }

                switch (frame.Kind)
                {
                    case AttachFrameKind.Output:
                        OutputReceived?.Invoke(this, frame.Payload);
                        break;
                    case AttachFrameKind.InputControlledBy:
                        InputControlChanged?.Invoke(this, frame.Payload);
                        break;
                    case AttachFrameKind.Error:
                        ErrorReceived?.Invoke(this, frame.Payload);
                        break;
                    default:
                        OutputReceived?.Invoke(this, frame.Payload);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to parse attach stream message; forwarding raw");
                OutputReceived?.Invoke(this, message);
            }
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
        public async Task AttachToContainerAsync(string serverId, string? containerId = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serverId);

            if (_hubConnection.State != HubConnectionState.Connected)
                throw new InvalidOperationException("Not connected to hub. Call ConnectAsync first.");

            if (_attachedContainerId != null)
                throw new InvalidOperationException($"Already attached to container {_attachedContainerId}");

            _logger?.LogInformation(
                "Subscribing to shared attach stream for server {ServerId}, container {ContainerId}",
                serverId, containerId ?? "<resolved>");

            _streamCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var streamCancellation = _streamCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await foreach (var message in _hubConnection.StreamAsync<string>(
                        "SubscribeToContainer",
                        serverId,
                        containerId,
                        false,
                        streamCancellation))
                    {
                        ProcessMessage(message);
                    }

                    Disconnected?.Invoke(this, "Attach stream ended");
                }
                catch (OperationCanceledException)
                {
                    Disconnected?.Invoke(this, "Attach stream cancelled");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Attach stream failed for server {ServerId}", serverId);
                    ErrorReceived?.Invoke(this, $"Attach stream failed: {ex.Message}");
                    Disconnected?.Invoke(this, ex.Message);
                }
                finally
                {
                    _attachedContainerId = containerId;
                }
            }, CancellationToken.None);

            // Give the stream a moment to start and resolve the container.
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(containerId))
            {
                _attachedContainerId = containerId;
                Connected?.Invoke(this, containerId);
            }
        }

        /// <inheritdoc/>
        public async Task SendInputAsync(string input, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(input))
                return;

            if (_hubConnection.State != HubConnectionState.Connected)
                throw new InvalidOperationException("Not connected to hub");

            if (string.IsNullOrEmpty(_attachedContainerId))
                throw new InvalidOperationException("Not attached to any container.");

            _logger?.LogTrace("Sending input: {Length} chars", input.Length);

            try
            {
                await _hubConnection.InvokeAsync<bool>(
                    "SendInput",
                    _attachedContainerId,
                    input,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error sending attach input");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task DisconnectFromContainerAsync(CancellationToken cancellationToken = default)
        {
            if (_hubConnection.State != HubConnectionState.Connected)
                return;

            if (string.IsNullOrEmpty(_attachedContainerId))
                return;

            _logger?.LogInformation("Disconnecting from shared attach stream: {ContainerId}", _attachedContainerId);

            try
            {
                _streamCts?.Cancel();
                await _hubConnection.InvokeAsync("DisconnectFromContainer", _attachedContainerId, cancellationToken);
                _attachedContainerId = null;
                _logger?.LogInformation("Successfully disconnected from shared attach stream");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disconnecting from shared attach stream");
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
                _streamCts?.Cancel();
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
                _streamCts?.Cancel();
                await DisconnectFromContainerAsync();
                await StopAsync();
                await _hubConnection.DisposeAsync();
                _streamCts?.Dispose();

                _logger?.LogInformation("ContainerConsoleClient disposed");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing ContainerConsoleClient");
            }
        }

        private enum AttachFrameKind
        {
            Output,
            InputControlledBy,
            Error
        }

        private sealed record AttachStreamMessage(AttachFrameKind Kind, string Payload);

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
