namespace GameServer.API.Interfaces;

/// <summary>
/// Aggregates real-time resource usage data from Node Agents and Docker Swarm
/// service state, then fans updates out to multiple SignalR subscribers.
/// A single agent stream is maintained per server/container; all clients monitoring
/// the same server share the same underlying stream.
/// </summary>
public interface IServerResourceAggregator
{
    /// <summary>
    /// Streams resource usage updates for a server until cancellation.
    /// The sequence is backed by a shared agent stream and service state polling.
    /// </summary>
    /// <param name="serverId">Server ID to monitor</param>
    /// <param name="intervalSeconds">Minimum interval between emitted updates</param>
    /// <param name="cancellationToken">Cancellation token</param>
    IAsyncEnumerable<Models.ServerResourceUsage> StreamResourceUsageAsync(
        string serverId,
        int intervalSeconds = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single resource snapshot for a server.
    /// </summary>
    /// <param name="serverId">Server ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Models.ServerResourceUsage?> GetSnapshotAsync(string serverId, CancellationToken cancellationToken = default);
}
