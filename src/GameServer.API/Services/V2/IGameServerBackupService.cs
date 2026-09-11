using System.Security.Claims;
using GameServer.API.Dtos.V2;

namespace GameServer.API.Services.V2;

public interface IGameServerBackupService
{
    Task<GameServerBackupDto> CreateBackupAsync(
        string serverId,
        CreateBackupRequestDto request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameServerBackupDto>> GetBackupsAsync(
        ClaimsPrincipal user,
        string? serverId = null,
        CancellationToken cancellationToken = default);

    Task<GameServerBackupDto?> GetBackupByIdAsync(
        int id,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<(Stream Stream, string ContentType, string FileName)> DownloadBackupAsync(
        int id,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<GameServerBackupDto> ExtendBackupRetentionAsync(
        int id,
        ExtendBackupRequestDto request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<GameServerBackupDto> UpdateSharingAsync(
        int id,
        UpdateBackupSharingRequestDto request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task DeleteBackupAsync(
        int id,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<int> CleanupExpiredBackupsAsync(
        CancellationToken cancellationToken = default);
}
