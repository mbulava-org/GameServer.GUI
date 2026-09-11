using GameServer.Web.Models.V2;

namespace GameServer.Web.Services.V2;

public interface IBackupsApiService
{
    Task<GameServerBackup> CreateBackupAsync(string serverId, CreateBackupRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameServerBackup>> GetBackupsAsync(string? serverId = null, CancellationToken cancellationToken = default);
    Task<GameServerBackup?> GetBackupByIdAsync(string backupId, CancellationToken cancellationToken = default);
    Task<byte[]> DownloadBackupAsync(string backupId, CancellationToken cancellationToken = default);
    Task<GameServerBackup> ExtendBackupAsync(string backupId, ExtendBackupRequest request, CancellationToken cancellationToken = default);
    Task<GameServerBackup> UpdateSharingAsync(string backupId, UpdateBackupSharingRequest request, CancellationToken cancellationToken = default);
    Task DeleteBackupAsync(string backupId, CancellationToken cancellationToken = default);
}
