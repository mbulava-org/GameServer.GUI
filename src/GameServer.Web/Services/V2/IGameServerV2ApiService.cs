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
}
