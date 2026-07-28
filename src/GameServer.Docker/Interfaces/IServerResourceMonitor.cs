namespace GameServer.Docker.Interfaces;

/// <summary>
/// Provides streaming and snapshot resource usage for a server.
/// This is the V2-compatible replacement for the removed legacy resource monitor.
/// </summary>
public interface IServerResourceMonitor
{
    /// <summary>
    /// Streams real-time resource usage for a server until cancellation.
    /// </summary>
    /// <param name="serverId">Server ID to monitor</param>
    /// <param name="cancellationToken">Cancellation token</param>
    IAsyncEnumerable<Models.ServerResourceUsage> StreamResourceUsageAsync(string serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single resource snapshot for a server.
    /// </summary>
    /// <param name="serverId">Server ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Resource usage snapshot or null if the server is not found or no data is available</returns>
    Task<Models.ServerResourceUsage?> GetSnapshotAsync(string serverId, CancellationToken cancellationToken = default);
}
