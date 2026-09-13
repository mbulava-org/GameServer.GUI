using System.Security.Claims;
using GameServer.API.Dtos.V2;

namespace GameServer.API.Services.V2;

/// <summary>
/// Handles checking and updating container images for V2 GameServers.
/// </summary>
public interface IContainerImageUpdateService
{
    /// <summary>
    /// Checks if a newer container image release/digest is available at the source registry for a game server.
    /// </summary>
    Task<ContainerImageUpdateStatusDto> CheckImageUpdateAsync(string serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets any cached container image update status for a server if already checked.
    /// </summary>
    ContainerImageUpdateStatusDto? GetCachedUpdateStatus(string serverId);

    /// <summary>
    /// Updates the container to the latest image tag from the remote registry and triggers redeployment.
    /// </summary>
    Task<GameServerDetailDto> UpdateContainerImageAsync(string serverId, ClaimsPrincipal? user = null, CancellationToken cancellationToken = default);
}
