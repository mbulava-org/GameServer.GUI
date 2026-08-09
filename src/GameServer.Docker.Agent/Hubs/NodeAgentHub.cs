using Microsoft.AspNetCore.SignalR;
using GameServer.Docker.Agent.Interfaces;
using Docker.DotNet.Models;
using System.Collections.Concurrent;

namespace GameServer.Docker.Agent.Hubs
{
    /// <summary>
    /// SignalR Hub for streaming real-time container statistics and events from Node Agent to Primary Service.
    /// Uses Docker's native streaming API with IProgress callbacks.
    /// </summary>
    public class NodeAgentHub : Hub
    {
        private readonly ILogger<NodeAgentHub> _logger;
        private readonly IContainerService _containerService;
        
        // Track active streams per connection
        private static readonly ConcurrentDictionary<string, StreamingSession> _activeSessions = new();
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new();

        public NodeAgentHub(
            ILogger<NodeAgentHub> logger,
            IContainerService containerService)
        {
            _logger = logger;
            _containerService = containerService;
        }

        /// <summary>
        /// Stream container stats using Docker's native streaming API.
        /// This method uses async streams (IAsyncEnumerable) for clean resource management.
        /// </summary>
        /// <param name="containerId">Container ID to monitor</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async IAsyncEnumerable<object> StreamContainerStats(
            string containerId, 
            [System.Runtime.CompilerServices.EnumeratorCancellation] System.Threading.CancellationToken cancellationToken)
        {
            var connectionId = Context.ConnectionId;
            _logger.LogTrace("Client {ConnectionId} starting stats stream for container {ContainerId}", 
                connectionId, containerId);

            var session = new StreamingSession
            {
                ConnectionId = connectionId,
                ContainerId = containerId,
                StartedAt = DateTime.UtcNow
            };

            _activeSessions[connectionId] = session;

            try
            {
                await foreach (var stats in _containerService.StreamContainerStatsAsync(containerId, cancellationToken))
                {
                    yield return stats;
                }
            }
            finally
            {
                _activeSessions.TryRemove(connectionId, out _);
                _logger.LogTrace("Stats stream ended for container {ContainerId} on connection {ConnectionId}",
                    containerId, connectionId);
            }
        }

        /// <summary>
        /// Get a single snapshot of container stats (non-streaming)
        /// </summary>
        public async Task<object?> GetContainerStatsSnapshot(string containerId)
        {
            _logger.LogDebug("Client {ConnectionId} requesting stats snapshot for container {ContainerId}",
                Context.ConnectionId, containerId);

            try
            {
                var stats = await _containerService.GetContainerStatsAsync(containerId);
                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stats snapshot for container {ContainerId}", containerId);
                throw;
            }
        }

        /// <summary>
        /// Get container logs
        /// </summary>
        public async Task<object?> GetContainerLogs(string containerId, int tailLines = 100)
        {
            _logger.LogDebug("Client {ConnectionId} requesting logs for container {ContainerId} (tail: {TailLines})",
                Context.ConnectionId, containerId, tailLines);

            try
            {
                var logs = await _containerService.GetContainerLogsAsync(containerId, tailLines);
                return new { containerId, logs };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting logs for container {ContainerId}", containerId);
                throw;
            }
        }

        /// <summary>
        /// Stream container logs in real-time using Docker's native log streaming API.
        /// This method uses async streams (IAsyncEnumerable) for efficient real-time log delivery.
        /// </summary>
        /// <param name="containerId">Container ID to stream logs from</param>
        /// <param name="follow">If true, continuously streams new logs. If false, returns historical logs only.</param>
        /// <param name="tailLines">Number of recent lines to include (0 for all)</param>
        /// <param name="timestamps">Include timestamps in log output</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async IAsyncEnumerable<string> StreamContainerLogs(
            string containerId,
            bool follow = true,
            int tailLines = 100,
            bool timestamps = true,
            [System.Runtime.CompilerServices.EnumeratorCancellation] System.Threading.CancellationToken cancellationToken = default)
        {
            var connectionId = Context.ConnectionId;
            _logger.LogInformation("Client {ConnectionId} starting log stream for container {ContainerId} (follow={Follow}, tail={Tail})",
                connectionId, containerId, follow, tailLines);

            try
            {
                await foreach (var logLine in _containerService.StreamContainerLogsAsync(
                    containerId, follow, tailLines, timestamps, cancellationToken))
                {
                    yield return logLine;
                }
            }
            finally
            {
                _logger.LogInformation("Log stream ended for container {ContainerId} on connection {ConnectionId}",
                    containerId, connectionId);
            }
        }

        /// <summary>
        /// Called when a client disconnects
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;
            
            
            if (_activeSessions.TryRemove(connectionId, out var session))
            {
                _logger.LogTrace("Client {ConnectionId} disconnected, cleaning up streaming session for container {ContainerId}",
                    connectionId, session.ContainerId);
            }

            if (_cancellationTokens.TryRemove(connectionId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }

            await base.OnDisconnectedAsync(exception);
        }

        private class StreamingSession
        {
            public string ConnectionId { get; set; } = string.Empty;
            public string ContainerId { get; set; } = string.Empty;
            public DateTime StartedAt { get; set; }
        }
    }
}
