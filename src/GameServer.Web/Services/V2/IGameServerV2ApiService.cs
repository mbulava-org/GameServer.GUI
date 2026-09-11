using GameServer.Web.Models.V2;

namespace GameServer.Web.Services.V2;

/// <summary>
/// Web-side client abstraction for the V2 GameServer API.
/// </summary>
public interface IGameServerV2ApiService
{
    /// <summary>
    /// Gets the V2 GameServer list.
    /// </summary>
    Task<IReadOnlyList<GameServerListItem>> GetListAsync(bool includeDeleted = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the V2 GameServer detail payload.
    /// </summary>
    Task<GameServerDetail?> GetByServerIdAsync(string serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a V2 GameServer request.
    /// </summary>
    Task<GameServerValidationResult> ValidateAsync(SaveGameServerRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Produces a dry-run preview of the Swarm service that would be created for a request.
    /// </summary>
    Task<GameServerDeploymentPreview> PreviewAsync(SaveGameServerRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the supplied published ports are available for the given server.
    /// </summary>
    Task<GameServerPortAvailabilityResult> CheckPortAvailabilityAsync(GameServerPortAvailabilityRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a V2 GameServer.
    /// </summary>
    Task<GameServerDetail> CreateAsync(SaveGameServerRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a V2 GameServer.
    /// </summary>
    Task<GameServerDetail> UpdateAsync(string serverId, SaveGameServerRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the Swarm service for a V2 GameServer.
    /// </summary>
    Task<GameServerDetail> StartAsync(string serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the Swarm service for a V2 GameServer.
    /// </summary>
    Task<GameServerDetail> StopAsync(string serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restarts the Swarm service for a V2 GameServer.
    /// </summary>
    Task<GameServerDetail> RestartAsync(string serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Redeploys and updates the Swarm service for a V2 GameServer.
    /// </summary>
    Task<GameServerDetail> RedeployAsync(string serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a V2 GameServer.
    /// </summary>
    Task DeleteAsync(string serverId, bool softDelete = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the historical resource utilization records for a V2 GameServer.
    /// </summary>
    Task<IReadOnlyList<GameServerResourceHistoryItem>> GetResourceHistoryAsync(string serverId, DateTime? from = null, DateTime? to = null, int limit = 5000, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets per-group access rows for a server, scoped to the groups the current user belongs to.
    /// Only the server creator or an admin is allowed to invoke this.
    /// </summary>
    Task<IReadOnlyList<ServerGroupAccessRow>> GetServerGroupAccessAsync(string serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets per-group access for a server, scoped to the groups the current user belongs to.
    /// Only the server creator or an admin is allowed to invoke this.
    /// </summary>
    Task<IReadOnlyList<ServerGroupAccessRow>> SetServerGroupAccessAsync(string serverId, SetServerGroupAccessRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of Swarm task/container instances for a V2 GameServer.
    /// </summary>
    Task<IReadOnlyList<GameServerInstanceInfo>> GetInstancesAsync(string serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets logs for a specific instance or the active instance of a V2 GameServer.
    /// </summary>
    Task<string> GetLogsAsync(string serverId, string? instanceId = null, int tail = 200, CancellationToken cancellationToken = default);
}

