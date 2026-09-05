using GameServer.API.Models;

namespace GameServer.API.Services.V2;

public interface IGameServerResourceCollector
{
    /// <summary>
    /// Triggers immediate resource collection for a specific game server (e.g. on start, restart, or deploy).
    /// </summary>
    Task TriggerImmediateCollectionAsync(string serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent in-memory cached resource usage snapshot for a server, if available.
    /// </summary>
    ServerResourceUsage? GetCachedUsage(string serverId);

    /// <summary>
    /// Flushes all currently queued resource utilization records to the database.
    /// </summary>
    Task FlushAsync(CancellationToken cancellationToken = default);
}
