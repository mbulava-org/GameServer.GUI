using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GameServer.API.Client.Interfaces
{
    /// <summary>
    /// Interface for real-time server resource monitoring via SignalR.
    /// Streams resource usage updates (CPU, memory, network, disk, etc.).
    /// </summary>
    public interface IResourceMonitoringClient : IAsyncDisposable
    {
        /// <summary>
        /// Event raised when resource usage update is received for a server
        /// </summary>
        event EventHandler<ServerResourceUsage>? ResourceUpdateReceived;

        /// <summary>
        /// Event raised when batch of resource updates is received (multi-server monitoring)
        /// </summary>
        event EventHandler<IEnumerable<ServerResourceUsage>>? ResourceUpdateBatchReceived;

        /// <summary>
        /// Event raised when subscription is confirmed
        /// </summary>
        event EventHandler<(string ServerId, int IntervalSeconds)>? Subscribed;

        /// <summary>
        /// Event raised when multi-server subscription is confirmed
        /// </summary>
        event EventHandler<(string[] ServerIds, int IntervalSeconds)>? SubscribedMultiple;

        /// <summary>
        /// Event raised when unsubscribed from monitoring
        /// </summary>
        event EventHandler? Unsubscribed;

        /// <summary>
        /// Event raised when monitoring interval is updated
        /// </summary>
        event EventHandler<int>? IntervalUpdated;

        /// <summary>
        /// Event raised when an error occurs
        /// </summary>
        event EventHandler<string>? ErrorReceived;

        /// <summary>
        /// Gets whether the client is currently connected to the hub
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Gets the currently monitored server ID (single-server mode), or null
        /// </summary>
        string? MonitoredServerId { get; }

        /// <summary>
        /// Gets the currently monitored server IDs (multi-server mode), or null
        /// </summary>
        IReadOnlyList<string>? MonitoredServerIds { get; }

        /// <summary>
        /// Gets the current monitoring interval in seconds
        /// </summary>
        int? CurrentIntervalSeconds { get; }

        /// <summary>
        /// Connects to the SignalR hub
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        Task ConnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Subscribe to resource updates for a specific server
        /// </summary>
        /// <param name="serverId">Server ID to monitor</param>
        /// <param name="intervalSeconds">Update interval in seconds (default: 5, min: 1, max: 60)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SubscribeToServerAsync(string serverId, int intervalSeconds = 5, CancellationToken cancellationToken = default);

        /// <summary>
        /// Subscribe to resource updates for multiple servers
        /// </summary>
        /// <param name="serverIds">Server IDs to monitor</param>
        /// <param name="intervalSeconds">Update interval in seconds (default: 5, min: 1, max: 60)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SubscribeToMultipleServersAsync(string[] serverIds, int intervalSeconds = 5, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get a single resource snapshot without subscribing
        /// </summary>
        /// <param name="serverId">Server ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Server resource usage or null if not found</returns>
        Task<ServerResourceUsage?> GetSnapshotAsync(string serverId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update the monitoring interval for current subscription
        /// </summary>
        /// <param name="intervalSeconds">New interval in seconds (min: 1, max: 60)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task UpdateIntervalAsync(int intervalSeconds, CancellationToken cancellationToken = default);

        /// <summary>
        /// Unsubscribe from current monitoring session
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        Task UnsubscribeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the SignalR connection
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        Task StopAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Server resource usage data
    /// Note: This mirrors GameServer.API.Models.ServerResourceUsage
    /// </summary>
    public class ServerResourceUsage
    {
        public string ServerId { get; set; } = string.Empty;
        public string ServerName { get; set; } = string.Empty;
        public string GameType { get; set; } = string.Empty;
        public bool IsRunning { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public double? CpuUsagePercent { get; set; }
        public long? MemoryUsageBytes { get; set; }
        public long? MemoryLimitBytes { get; set; }
        public double? MemoryUsagePercent { get; set; }
        public long? NetworkRxBytes { get; set; }
        public long? NetworkTxBytes { get; set; }
        public long? BlockReadBytes { get; set; }
        public long? BlockWriteBytes { get; set; }
        public int? Replicas { get; set; }
        public int? HealthyReplicas { get; set; }
        public string? ContainerId { get; set; }
        public string? NodeName { get; set; }
    }
}
