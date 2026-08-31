using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameServer.API.Client.Interfaces;
using GameServer.API.Client.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace GameServer.API.Client.Services
{
    /// <summary>
    /// SignalR client for real-time server resource monitoring.
    /// Streams resource usage updates from the GameServer.API SignalR hub.
    /// </summary>
    public class ResourceMonitoringClient : IResourceMonitoringClient
    {
        private readonly HubConnection _hubConnection;
        private readonly ILogger<ResourceMonitoringClient>? _logger;
        private string? _monitoredServerId;
        private IReadOnlyList<string>? _monitoredServerIds;
        private int? _currentIntervalSeconds;

        /// <inheritdoc/>
        public event EventHandler<Interfaces.ServerResourceUsage>? ResourceUpdateReceived;

        /// <inheritdoc/>
        public event EventHandler<IEnumerable<Interfaces.ServerResourceUsage>>? ResourceUpdateBatchReceived;

        /// <inheritdoc/>
        public event EventHandler<(string ServerId, int IntervalSeconds)>? Subscribed;

        /// <inheritdoc/>
        public event EventHandler<(string[] ServerIds, int IntervalSeconds)>? SubscribedMultiple;

        /// <inheritdoc/>
        public event EventHandler? Unsubscribed;

        /// <inheritdoc/>
        public event EventHandler<int>? IntervalUpdated;

        /// <inheritdoc/>
        public event EventHandler<string>? ErrorReceived;

        /// <inheritdoc/>
        public bool IsConnected => _hubConnection.State == HubConnectionState.Connected;

        /// <inheritdoc/>
        public string? MonitoredServerId => _monitoredServerId;

        /// <inheritdoc/>
        public IReadOnlyList<string>? MonitoredServerIds => _monitoredServerIds;

        /// <inheritdoc/>
        public int? CurrentIntervalSeconds => _currentIntervalSeconds;

        /// <summary>
        /// Creates a new instance of ResourceMonitoringClient
        /// </summary>
        /// <param name="hubUrl">SignalR hub URL (e.g., "https://your-server/hubs/resources")</param>
        /// <param name="logger">Optional logger</param>
        public ResourceMonitoringClient(string hubUrl, ILogger<ResourceMonitoringClient>? logger = null)
        {
            if (string.IsNullOrWhiteSpace(hubUrl))
                throw new ArgumentException("Hub URL cannot be null or empty", nameof(hubUrl));

            _logger = logger;

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect(new RetryPolicy())
                .Build();

            RegisterEventHandlers();

            _logger?.LogInformation("ResourceMonitoringClient created for hub: {HubUrl}", hubUrl);
        }

        /// <summary>
        /// Creates a new instance using a pre-configured HubConnection
        /// </summary>
        /// <param name="hubConnection">Pre-configured hub connection</param>
        /// <param name="logger">Optional logger</param>
        public ResourceMonitoringClient(HubConnection hubConnection, ILogger<ResourceMonitoringClient>? logger = null)
        {
            _hubConnection = hubConnection ?? throw new ArgumentNullException(nameof(hubConnection));
            _logger = logger;
            RegisterEventHandlers();
        }

        private static Interfaces.ServerResourceUsage ToInterfaceModel(HubResourceUsage usage)
        {
            return new Interfaces.ServerResourceUsage
            {
                ServerId = usage.ServerId,
                ServerName = usage.ServerId,
                GameType = string.Empty,
                IsRunning = usage.ServiceStatus == "Running",
                Status = usage.ServiceStatus,
                Timestamp = usage.Timestamp,
                CpuUsagePercent = usage.RealTimeStats?.CpuUsagePercent ?? usage.CpuUsagePercent,
                MemoryUsageBytes = usage.RealTimeStats != null ? (long?)usage.RealTimeStats.MemoryUsageBytes : usage.MemoryUsageBytes,
                MemoryLimitBytes = usage.RealTimeStats != null ? (long?)usage.RealTimeStats.MemoryLimitBytes : usage.MemoryLimitBytes,
                MemoryUsagePercent = usage.RealTimeStats?.MemoryUsagePercent ?? usage.MemoryUsagePercent,
                NetworkRxBytes = usage.RealTimeStats?.NetworkRxBytes ?? usage.NetworkRxBytes,
                NetworkTxBytes = usage.RealTimeStats?.NetworkTxBytes ?? usage.NetworkTxBytes,
                BlockReadBytes = usage.RealTimeStats?.BlockReadBytes ?? usage.BlockReadBytes,
                BlockWriteBytes = usage.RealTimeStats?.BlockWriteBytes ?? usage.BlockWriteBytes,
                Replicas = usage.DesiredReplicas,
                HealthyReplicas = usage.RunningReplicas,
                ContainerId = usage.ContainerIds?.FirstOrDefault(),
                NodeName = null
            };
        }

        private void RegisterEventHandlers()
        {
            // Single resource update - Use the auto-generated model from NSwag
            _hubConnection.On<HubResourceUsage>("ResourceUpdate", (usage) =>
            {
                _logger?.LogTrace("Received resource update for server {ServerId}", usage.ServerId);

                var interfaceModel = ToInterfaceModel(usage);

                ResourceUpdateReceived?.Invoke(this, interfaceModel);
            });

            // Batch of resource updates
            _hubConnection.On<IEnumerable<HubResourceUsage>>("ResourceUpdateBatch", (updates) =>
            {
                _logger?.LogTrace("Received resource update batch: {Count} servers", updates.Count());

                var interfaceModels = updates.Select(ToInterfaceModel).ToList();

                ResourceUpdateBatchReceived?.Invoke(this, interfaceModels);
            });

            // Subscribed to single server
            _hubConnection.On<string, int>("Subscribed", (serverId, intervalSeconds) =>
            {
                _logger?.LogInformation("Subscribed to server {ServerId} with {Interval}s interval",
                    serverId, intervalSeconds);
                _monitoredServerId = serverId;
                _monitoredServerIds = null;
                _currentIntervalSeconds = intervalSeconds;
                Subscribed?.Invoke(this, (serverId, intervalSeconds));
            });

            // Subscribed to multiple servers
            _hubConnection.On<string[], int>("SubscribedMultiple", (serverIds, intervalSeconds) =>
            {
                _logger?.LogInformation("Subscribed to {Count} servers with {Interval}s interval",
                    serverIds.Length, intervalSeconds);
                _monitoredServerId = null;
                _monitoredServerIds = serverIds;
                _currentIntervalSeconds = intervalSeconds;
                SubscribedMultiple?.Invoke(this, (serverIds, intervalSeconds));
            });

            // Unsubscribed
            _hubConnection.On("Unsubscribed", () =>
            {
                _logger?.LogInformation("Unsubscribed from monitoring");
                _monitoredServerId = null;
                _monitoredServerIds = null;
                _currentIntervalSeconds = null;
                Unsubscribed?.Invoke(this, EventArgs.Empty);
            });

            // Interval updated
            _hubConnection.On<int>("IntervalUpdated", (intervalSeconds) =>
            {
                _logger?.LogInformation("Monitoring interval updated to {Interval}s", intervalSeconds);
                _currentIntervalSeconds = intervalSeconds;
                IntervalUpdated?.Invoke(this, intervalSeconds);
            });

            // Error messages
            _hubConnection.On<string>("Error", (message) =>
            {
                _logger?.LogWarning("Received error: {Message}", message);
                ErrorReceived?.Invoke(this, message);
            });

            // Connection state changes
            _hubConnection.Closed += async (error) =>
            {
                _logger?.LogWarning(error, "Hub connection closed");
                _monitoredServerId = null;
                _monitoredServerIds = null;
                _currentIntervalSeconds = null;
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
                // Note: Will need to resubscribe after reconnection
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
        public async Task SubscribeToServerAsync(string serverId, int intervalSeconds = 5, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serverId))
                throw new ArgumentException("Server ID cannot be null or empty", nameof(serverId));

            if (intervalSeconds < 1 || intervalSeconds > 60)
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds), "Interval must be between 1 and 60 seconds");

            if (_hubConnection.State != HubConnectionState.Connected)
                throw new InvalidOperationException("Not connected to hub. Call ConnectAsync first.");

            _logger?.LogInformation("Subscribing to server {ServerId} with {Interval}s interval",
                serverId, intervalSeconds);

            try
            {
                await _hubConnection.InvokeAsync("SubscribeToServer", serverId, intervalSeconds, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error subscribing to server {ServerId}", serverId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task SubscribeToMultipleServersAsync(string[] serverIds, int intervalSeconds = 5, CancellationToken cancellationToken = default)
        {
            if (serverIds == null || serverIds.Length == 0)
                throw new ArgumentException("Server IDs cannot be null or empty", nameof(serverIds));

            if (intervalSeconds < 1 || intervalSeconds > 60)
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds), "Interval must be between 1 and 60 seconds");

            if (_hubConnection.State != HubConnectionState.Connected)
                throw new InvalidOperationException("Not connected to hub. Call ConnectAsync first.");

            _logger?.LogInformation("Subscribing to {Count} servers with {Interval}s interval",
                serverIds.Length, intervalSeconds);

            try
            {
                await _hubConnection.InvokeAsync("SubscribeToMultipleServers", serverIds, intervalSeconds, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error subscribing to multiple servers");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<Interfaces.ServerResourceUsage?> GetSnapshotAsync(string serverId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serverId))
                throw new ArgumentException("Server ID cannot be null or empty", nameof(serverId));

            if (_hubConnection.State != HubConnectionState.Connected)
                throw new InvalidOperationException("Not connected to hub. Call ConnectAsync first.");

            _logger?.LogDebug("Getting resource snapshot for server {ServerId}", serverId);

            try
            {
                // Call hub and get the hub payload model
                var result = await _hubConnection.InvokeAsync<HubResourceUsage?>(
                    "GetSnapshot",
                    serverId,
                    cancellationToken);

                if (result == null)
                {
                    _logger?.LogWarning("Snapshot returned null for server {ServerId}", serverId);
                    return null;
                }

                return ToInterfaceModel(result);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting snapshot for server {ServerId}", serverId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task UpdateIntervalAsync(int intervalSeconds, CancellationToken cancellationToken = default)
        {
            if (intervalSeconds < 1 || intervalSeconds > 60)
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds), "Interval must be between 1 and 60 seconds");

            if (_hubConnection.State != HubConnectionState.Connected)
                throw new InvalidOperationException("Not connected to hub");

            if (_monitoredServerId == null && _monitoredServerIds == null)
                throw new InvalidOperationException("Not currently subscribed to any servers");

            _logger?.LogInformation("Updating monitoring interval to {Interval}s", intervalSeconds);

            try
            {
                await _hubConnection.InvokeAsync("UpdateInterval", intervalSeconds, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating interval");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task UnsubscribeAsync(CancellationToken cancellationToken = default)
        {
            if (_hubConnection.State != HubConnectionState.Connected)
                return;

            if (_monitoredServerId == null && _monitoredServerIds == null)
                return;

            _logger?.LogInformation("Unsubscribing from resource monitoring");

            try
            {
                await _hubConnection.InvokeAsync("Unsubscribe", cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error unsubscribing");
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
                _monitoredServerId = null;
                _monitoredServerIds = null;
                _currentIntervalSeconds = null;
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
                await UnsubscribeAsync();
                await StopAsync();
                await _hubConnection.DisposeAsync();
                _logger?.LogInformation("ResourceMonitoringClient disposed");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing ResourceMonitoringClient");
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
