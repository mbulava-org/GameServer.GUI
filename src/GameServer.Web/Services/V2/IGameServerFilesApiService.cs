using GameServer.Web.Models.V2;

namespace GameServer.Web.Services.V2;

public interface IGameServerFilesApiService
{
    Task<IReadOnlyList<FileItem>> ListFilesAsync(
        string serverId,
        string volumePath,
        string? subPath = null,
        CancellationToken cancellationToken = default);

    Task<string> GetContentAsync(
        string serverId,
        string volumePath,
        string subPath,
        CancellationToken cancellationToken = default);

    Task SaveContentAsync(
        string serverId,
        string volumePath,
        string subPath,
        string content,
        CancellationToken cancellationToken = default);

    Task<byte[]> DownloadAsync(
        string serverId,
        string volumePath,
        string subPath,
        CancellationToken cancellationToken = default);

    Task UploadAsync(
        string serverId,
        string volumePath,
        string? subPath,
        string fileName,
        Stream contentStream,
        CancellationToken cancellationToken = default);

    Task CreateDirectoryAsync(
        string serverId,
        string volumePath,
        string subPath,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string serverId,
        string volumePath,
        string subPath,
        bool recursive = false,
        CancellationToken cancellationToken = default);
}
