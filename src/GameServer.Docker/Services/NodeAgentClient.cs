using Microsoft.AspNetCore.SignalR.Client;
using System.Runtime.CompilerServices;

namespace GameServer.Docker.Services
{
    /// <summary>
    /// SignalR client for connecting to Node Agent hubs for real-time container data.
    /// Manages persistent connections and provides streaming methods for logs and stats.
    /// </summary>
    public class NodeAgentClient : IAsyncDisposable
    {
        private readonly ILogger<NodeAgentClient> _logger;
        private readonly Dictionary<string, HubConnection> _connections = new();
        private readonly SemaphoreSlim _lock = new(1, 1);

        public NodeAgentClient(ILogger<NodeAgentClient> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Get or create a connection to a Node Agent
        /// </summary>
        private async Task<HubConnection> GetOrCreateConnectionAsync(string agentUrl, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (_connections.TryGetValue(agentUrl, out var existing))
                {
                    // Check if connection is still valid
                    if (existing.State == HubConnectionState.Connected)
                    {
                        return existing;
                    }
                    
                    // Connection is dead, remove it
                    _logger.LogWarning("Removing stale connection to {AgentUrl}", agentUrl);
                    _connections.Remove(agentUrl);
                    try
                    {
                        await existing.DisposeAsync();
                    }
                    catch { /* Ignore disposal errors */ }
                }

                _logger.LogInformation("Creating SignalR connection to Node Agent at {AgentUrl}", agentUrl);

                var connection = new HubConnectionBuilder()
                    .WithUrl($"{agentUrl}/hubs/nodeagent")
                    .WithAutomaticReconnect(new[] 
                    { 
                        TimeSpan.Zero,           // Retry immediately
                        TimeSpan.FromSeconds(2), // Then after 2s
                        TimeSpan.FromSeconds(5), // Then after 5s
                        TimeSpan.FromSeconds(10) // Then after 10s
                    })
                    .AddJsonProtocol(options =>
                    {
                        options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
                    })
                    .Build();

                // Handle connection closed
                connection.Closed += async (error) =>
                {
                    if (error != null)
                    {
                        _logger.LogWarning(error, "Connection to {AgentUrl} closed with error", agentUrl);
                    }
                    else
                    {
                        _logger.LogInformation("Connection to {AgentUrl} closed gracefully", agentUrl);
                    }
                    
                    await _lock.WaitAsync();
                    try
                    {
                        _connections.Remove(agentUrl);
                    }
                    finally
                    {
                        _lock.Release();
                    }
                };

                // Handle reconnecting
                connection.Reconnecting += error =>
                {
                    _logger.LogWarning(error, "Reconnecting to {AgentUrl}...", agentUrl);
                    return Task.CompletedTask;
                };

                // Handle reconnected
                connection.Reconnected += connectionId =>
                {
                    _logger.LogInformation("Reconnected to {AgentUrl} with connection ID {ConnectionId}", 
                        agentUrl, connectionId);
                    return Task.CompletedTask;
                };

                await connection.StartAsync(cancellationToken);
                _connections[agentUrl] = connection;

                _logger.LogInformation("Successfully connected to Node Agent at {AgentUrl}", agentUrl);
                return connection;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Stream container logs in real-time from a Node Agent
        /// </summary>
        /// <param name="agentUrl">Node Agent URL (e.g., http://node1:5000)</param>
        /// <param name="containerId">Container ID</param>
        /// <param name="follow">Continuously stream new logs</param>
        /// <param name="tailLines">Number of recent lines to include</param>
        /// <param name="timestamps">Include timestamps</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async IAsyncEnumerable<string> StreamContainerLogsAsync(
            string agentUrl,
            string containerId,
            bool follow = true,
            int tailLines = 100,
            bool timestamps = true,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting log stream from {AgentUrl} for container {ContainerId} (follow={Follow}, tail={Tail})", 
                agentUrl, containerId, follow, tailLines);

            HubConnection connection;
            try
            {
                connection = await GetOrCreateConnectionAsync(agentUrl, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Node Agent at {AgentUrl}", agentUrl);
                throw;
            }

            await foreach (var logLine in connection.StreamAsync<string>(
                "StreamContainerLogs",
                containerId,
                follow,
                tailLines,
                timestamps,
                cancellationToken))
            {
                yield return logLine;
            }

            _logger.LogInformation("Log stream ended for container {ContainerId} from {AgentUrl}", 
                containerId, agentUrl);
        }

        /// <summary>
        /// Stream container statistics in real-time from a Node Agent
        /// </summary>
        /// <param name="agentUrl">Node Agent URL (e.g., http://node1:5000)</param>
        /// <param name="containerId">Container ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async IAsyncEnumerable<object> StreamContainerStatsAsync(
            string agentUrl,
            string containerId,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting stats stream from {AgentUrl} for container {ContainerId}",
                agentUrl, containerId);

            HubConnection connection;
            try
            {
                connection = await GetOrCreateConnectionAsync(agentUrl, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Node Agent at {AgentUrl}", agentUrl);
                throw;
            }

            await foreach (var stats in connection.StreamAsync<object>(
                "StreamContainerStats",
                containerId,
                cancellationToken))
            {
                yield return stats;
            }

            _logger.LogInformation("Stats stream ended for container {ContainerId} from {AgentUrl}",
                containerId, agentUrl);
        }

        /// <summary>
        /// Get a single snapshot of container stats (non-streaming)
        /// </summary>
        public async Task<object?> GetContainerStatsSnapshotAsync(
            string agentUrl,
            string containerId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Getting stats snapshot from {AgentUrl} for container {ContainerId}",
                agentUrl, containerId);

            var connection = await GetOrCreateConnectionAsync(agentUrl, cancellationToken);
            return await connection.InvokeAsync<object?>(
                "GetContainerStatsSnapshot",
                containerId,
                cancellationToken);
        }

        /// <summary>
        /// Get container logs (batch, non-streaming)
        /// </summary>
        public async Task<object?> GetContainerLogsAsync(
            string agentUrl,
            string containerId,
            int tailLines = 100,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Getting logs from {AgentUrl} for container {ContainerId} (tail: {TailLines})",
                agentUrl, containerId, tailLines);

            var connection = await GetOrCreateConnectionAsync(agentUrl, cancellationToken);
            return await connection.InvokeAsync<object?>(
                "GetContainerLogs",
                containerId,
                tailLines,
                cancellationToken);
        }

        /// <summary>
        /// Close and dispose all connections
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await _lock.WaitAsync();
            try
            {
                _logger.LogInformation("Disposing NodeAgentClient with {Count} connections", _connections.Count);

                foreach (var (url, connection) in _connections.ToArray())
                {
                    try
                    {
                        _logger.LogDebug("Closing connection to {AgentUrl}", url);
                        await connection.StopAsync();
                        await connection.DisposeAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error disposing connection to {AgentUrl}", url);
                    }
                }
                _connections.Clear();
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
