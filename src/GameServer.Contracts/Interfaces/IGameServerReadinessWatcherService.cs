namespace GameServer.API.Interfaces;

/// <summary>
/// Monitors running game server log streams to detect when a server has finished initializing
/// based on its GameType revision's configured <c>ReadyLogPattern</c>.
/// </summary>
public interface IGameServerReadinessWatcherService
{
    /// <summary>
    /// Ensures that a readiness watch is active for the given server ID.
    /// If no ready log pattern is configured for the server's revision, the server is marked ready immediately.
    /// </summary>
    Task EnsureWatchingAsync(string serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the server is currently marked as ready for its active run.
    /// </summary>
    bool IsServerReady(string serverId);

    /// <summary>
    /// Explicitly marks the server as ready.
    /// </summary>
    void MarkReady(string serverId);

    /// <summary>
    /// Resets the readiness state for a server (e.g. when stopped or restarting).
    /// </summary>
    void ResetReadiness(string serverId);
}
