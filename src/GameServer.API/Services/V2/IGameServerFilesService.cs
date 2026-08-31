using GameServer.API.Dtos.V2;

namespace GameServer.API.Services.V2;

public interface IGameServerFilesService
{
    Task<IReadOnlyList<FileItemDto>> ListFilesAsync(
        string serverId,
        string volumeContainerPath,
        string? subPath = null,
        CancellationToken cancellationToken = default);

    Task<(Stream Stream, string ContentType, string FileName)> GetFileStreamAsync(
        string serverId,
        string volumeContainerPath,
        string subPath,
        CancellationToken cancellationToken = default);

    Task<string> GetFileContentTextAsync(
        string serverId,
        string volumeContainerPath,
        string subPath,
        CancellationToken cancellationToken = default);

    Task SaveFileContentTextAsync(
        string serverId,
        string volumeContainerPath,
        string subPath,
        string content,
        CancellationToken cancellationToken = default);

    Task UploadFileAsync(
        string serverId,
        string volumeContainerPath,
        string? subPath,
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default);

    Task CreateDirectoryAsync(
        string serverId,
        string volumeContainerPath,
        string subPath,
        CancellationToken cancellationToken = default);

    Task DeleteFileOrDirectoryAsync(
        string serverId,
        string volumeContainerPath,
        string subPath,
        bool recursive = false,
        CancellationToken cancellationToken = default);
}
