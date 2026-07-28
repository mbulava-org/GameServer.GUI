using System;
using System.Threading;
using System.Threading.Tasks;
using GameServer.Docker.Client.Interfaces;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace GameServer.Docker.Client.Services
{
    /// <summary>
    /// SignalR client for an interactive, per-user container exec session.
    /// Targets the /hubs/terminal endpoint. Output, errors, and session events
    /// are delivered only to the owning connection.
    /// </summary>
    public class ContainerTerminalClient : IContainerTerminalClient
    {
        private readonly HubConnection _hubConnection;
        private readonly ILogger<ContainerTerminalClient>? _logger;
        private string? _containerId;

        /// <inheritdoc/>
        public event EventHandler<string>? OutputReceived;

        /// <inheritdoc/>
        public event EventHandler<string>? ErrorReceived;

        /// <inheritdoc/>
        public event EventHandler<string>? SessionStarted;

        /// <inheritdoc/>
        public event EventHandler<string>? Connected;

        /// <inheritdoc/>
        public event EventHandler<string>? Disconnected;

        /// <inheritdoc/>
        public bool IsConnected => _hubConnection.State == HubConnectionState.Connected;

        /// <inheritdoc/>
        public string? ContainerId => _containerId;

        /// <inheritdoc/>
        public string? ConnectionId => _hubConnection.ConnectionId;

        /// <summary>
        /// Creates a new instance of ContainerTerminalClient
        /// </summary>
        /// <param name="hubUrl">SignalR hub URL (e.g., "https://your-server/hubs/terminal")</param>
        /// <param name="logger">Optional logger</param>
        public ContainerTerminalClient(string hubUrl, ILogger<ContainerTerminalClient>? logger = null)
        {
            if (string.IsNullOrWhiteSpace(hubUrl))
                throw new ArgumentException("Hub URL cannot be null or empty", nameof(hubUrl));

            _logger = logger;

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect(new RetryPolicy())
                .Build();

            RegisterEventHandlers();

            _logger?.LogInformation("ContainerTerminalClient created for hub: {HubUrl}", hubUrl);
        }

        /// <summary>
        /// Creates a new instance using a pre-configured HubConnection
        /// </summary>
        /// <param name="hubConnection">Pre-configured hub connection</param>
        /// <param name="logger">Optional logger</param>
        public ContainerTerminalClient(HubConnection hubConnection, ILogger<ContainerTerminalClient>? logger = null)
        {
            _hubConnection = hubConnection ?? throw new ArgumentNullException(nameof(hubConnection));
            _logger = logger;
            RegisterEventHandlers();
        }

        private void RegisterEventHandlers()
        {
            _hubConnection.On<string>("Output", data =>
            {
                _logger?.LogTrace("Terminal output received: {Length} chars", data?.Length ?? 0);
                OutputReceived?.Invoke(this, data ?? string.Empty);
            });

            _hubConnection.On<string>("SessionStarted", sessionId =>
            {
                _logger?.LogInformation("Terminal session started: {SessionId}", sessionId);
                SessionStarted?.Invoke(this, sessionId ?? string.Empty);
            });

            _hubConnection.On<string>("Connected", containerId =>
            {
                _logger?.LogInformation("Terminal connected to container: {ContainerId}", containerId);
                _containerId = containerId;
                Connected?.Invoke(this, containerId ?? string.Empty);
            });

            _hubConnection.On<string>("Error", error =>
            {
                _logger?.LogError("Terminal error received: {Error}", error);
                ErrorReceived?.Invoke(this, error ?? "Unknown error");
            });

            _hubConnection.Closed += error =>
            {
                _logger?.LogWarning(error, "Terminal hub connection closed");
                _containerId = null;
                Disconnected?.Invoke(this, error?.Message ?? "Connection closed");
                return Task.CompletedTask;
            };

            _hubConnection.Reconnecting += error =>
            {
                _logger?.LogWarning(error, "Terminal hub connection reconnecting");
                return Task.CompletedTask;
            };

            _hubConnection.Reconnected += connectionId =>
            {
                _logger?.LogInformation("Terminal hub connection reconnected: {ConnectionId}", connectionId);
                return Task.CompletedTask;
            };
        }

        /// <inheritdoc/>
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_hubConnection.State == HubConnectionState.Connected)
            {
                _logger?.LogDebug("Already connected to terminal hub");
                return;
            }

            _logger?.LogInformation("Connecting to terminal hub...");
            await _hubConnection.StartAsync(cancellationToken);
            _logger?.LogInformation("Successfully connected to terminal hub");
        }

        /// <inheritdoc/>
        public async Task<bool> StartExecSessionAsync(string containerId, string shell = "/bin/sh", CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(containerId);
            ArgumentException.ThrowIfNullOrWhiteSpace(shell);

            if (_hubConnection.State != HubConnectionState.Connected)
                throw new InvalidOperationException("Not connected to hub. Call ConnectAsync first.");

            _logger?.LogInformation(
                "Starting exec session for container {ContainerId} with shell {Shell}",
                containerId, shell);

            return await _hubConnection.InvokeAsync<bool>(
                "StartExecSession",
                containerId,
                shell,
                cancellationToken);
        }

        /// <inheritdoc/>
        public async Task SendInputAsync(string input, CancellationToken cancellationToken = default)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            if (_hubConnection.State != HubConnectionState.Connected)
                throw new InvalidOperationException("Not connected to hub");

            _logger?.LogTrace("Sending terminal input: {Length} chars", input.Length);

            await _hubConnection.InvokeAsync(
                "SendInput",
                ConnectionId ?? string.Empty,
                input,
                cancellationToken);
        }

        /// <inheritdoc/>
        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            if (_hubConnection.State != HubConnectionState.Connected)
                return;

            _logger?.LogInformation("Disconnecting from terminal session");

            try
            {
                await _hubConnection.InvokeAsync("Disconnect", cancellationToken);
                _containerId = null;
                _logger?.LogInformation("Successfully disconnected from terminal session");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disconnecting from terminal session");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (_hubConnection.State == HubConnectionState.Disconnected)
                return;

            _logger?.LogInformation("Stopping terminal hub connection");

            try
            {
                await _hubConnection.StopAsync(cancellationToken);
                _containerId = null;
                _logger?.LogInformation("Terminal hub connection stopped");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error stopping terminal hub connection");
                throw;
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            try
            {
                await DisconnectAsync();
                await StopAsync();
                await _hubConnection.DisposeAsync();

                _logger?.LogInformation("ContainerTerminalClient disposed");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing ContainerTerminalClient");
            }
        }

        /// <summary>
        /// Custom retry policy for SignalR reconnection
        /// </summary>
        private class RetryPolicy : IRetryPolicy
        {
            public TimeSpan? NextRetryDelay(RetryContext retryContext)
            {
                return retryContext.PreviousRetryCount switch
                {
                    0 => TimeSpan.Zero,
                    1 => TimeSpan.FromSeconds(2),
                    2 => TimeSpan.FromSeconds(10),
                    3 => TimeSpan.FromSeconds(30),
                    _ => null
                };
            }
        }
    }
}
