using GameServer.API.Dtos.V2;
using GameServer.API.Models.V2;
using GameServer.API.Repositories.V2;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;

namespace GameServer.API.Services.V2;

public sealed class GameServerFilesService(
    IGameServerRepository gameServerRepository,
    IGameTypeRepository gameTypeRepository,
    IMountTypeConfigRepository mountTypeConfigRepository,
    ILogger<GameServerFilesService> logger)
    : IGameServerFilesService
{
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

    public async Task<IReadOnlyList<FileItemDto>> ListFilesAsync(
        string serverId,
        string volumeContainerPath,
        string? subPath = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (volumeRoot, relativeBase) = await ResolveTargetDirectoryAsync(serverId, volumeContainerPath, subPath, cancellationToken).ConfigureAwait(false);

        if (!Directory.Exists(volumeRoot))
        {
            return [];
        }

        var dirInfo = new DirectoryInfo(volumeRoot);
        var result = new List<FileItemDto>();

        foreach (var dir in dirInfo.GetDirectories())
        {
            var relPath = string.IsNullOrEmpty(relativeBase) ? $"/{dir.Name}" : $"{relativeBase}/{dir.Name}";
            result.Add(new FileItemDto
            {
                Name = dir.Name,
                Path = relPath,
                IsDirectory = true,
                Size = 0,
                LastModified = dir.LastWriteTimeUtc,
                Extension = null,
                Permissions = "drwxr-xr-x"
            });
        }

        foreach (var file in dirInfo.GetFiles())
        {
            var relPath = string.IsNullOrEmpty(relativeBase) ? $"/{file.Name}" : $"{relativeBase}/{file.Name}";
            result.Add(new FileItemDto
            {
                Name = file.Name,
                Path = relPath,
                IsDirectory = false,
                Size = file.Length,
                LastModified = file.LastWriteTimeUtc,
                Extension = file.Extension,
                Permissions = "-rw-r--r--"
            });
        }

        return result.OrderByDescending(item => item.IsDirectory).ThenBy(item => item.Name).ToList();
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> GetFileStreamAsync(
        string serverId,
        string volumeContainerPath,
        string subPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var filePath = await ResolveTargetFilePathAsync(serverId, volumeContainerPath, subPath, cancellationToken).ConfigureAwait(false);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {subPath}");
        }

        var fileName = Path.GetFileName(filePath);
        if (!ContentTypeProvider.TryGetContentType(fileName, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        return (stream, contentType, fileName);
    }

    public async Task<string> GetFileContentTextAsync(
        string serverId,
        string volumeContainerPath,
        string subPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var filePath = await ResolveTargetFilePathAsync(serverId, volumeContainerPath, subPath, cancellationToken).ConfigureAwait(false);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {subPath}");
        }

        return await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveFileContentTextAsync(
        string serverId,
        string volumeContainerPath,
        string subPath,
        string content,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var filePath = await ResolveTargetFilePathAsync(serverId, volumeContainerPath, subPath, cancellationToken).ConfigureAwait(false);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(filePath, content, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Saved text file {FilePath} for server {ServerId}", filePath, serverId);
    }

    public async Task UploadFileAsync(
        string serverId,
        string volumeContainerPath,
        string? subPath,
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        cancellationToken.ThrowIfCancellationRequested();

        var safeFileName = Path.GetFileName(fileName);
        var (dirPath, _) = await ResolveTargetDirectoryAsync(serverId, volumeContainerPath, subPath, cancellationToken).ConfigureAwait(false);

        if (!Directory.Exists(dirPath))
        {
            Directory.CreateDirectory(dirPath);
        }

        var targetFilePath = Path.Combine(dirPath, safeFileName);
        using var targetStream = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(targetStream, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Uploaded file {FileName} to {DirPath} for server {ServerId}", safeFileName, dirPath, serverId);
    }

    public async Task CreateDirectoryAsync(
        string serverId,
        string volumeContainerPath,
        string subPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (dirPath, _) = await ResolveTargetDirectoryAsync(serverId, volumeContainerPath, subPath, cancellationToken).ConfigureAwait(false);

        if (!Directory.Exists(dirPath))
        {
            Directory.CreateDirectory(dirPath);
            logger.LogInformation("Created directory {DirPath} for server {ServerId}", dirPath, serverId);
        }
    }

    public async Task DeleteFileOrDirectoryAsync(
        string serverId,
        string volumeContainerPath,
        string subPath,
        bool recursive = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var targetPath = await ResolveTargetFilePathAsync(serverId, volumeContainerPath, subPath, cancellationToken).ConfigureAwait(false);

        if (Directory.Exists(targetPath))
        {
            Directory.Delete(targetPath, recursive);
            logger.LogInformation("Deleted directory {Path} for server {ServerId}", targetPath, serverId);
            return;
        }

        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
            logger.LogInformation("Deleted file {Path} for server {ServerId}", targetPath, serverId);
            return;
        }

        throw new FileNotFoundException($"Item not found: {subPath}");
    }

    private async Task<(string ResolvedPath, string RelativeBase)> ResolveTargetDirectoryAsync(
        string serverId,
        string volumeContainerPath,
        string? subPath,
        CancellationToken cancellationToken)
    {
        var localVolumeRoot = await GetLocalVolumeRootAsync(serverId, volumeContainerPath, cancellationToken).ConfigureAwait(false);
        var normalizedSubPath = NormalizeSubPath(subPath);

        var combined = string.IsNullOrEmpty(normalizedSubPath)
            ? localVolumeRoot
            : Path.Combine(localVolumeRoot, normalizedSubPath.Replace('/', Path.DirectorySeparatorChar));

        ValidatePathSafety(localVolumeRoot, combined);
        var relativeBase = string.IsNullOrEmpty(normalizedSubPath) ? string.Empty : $"/{normalizedSubPath}";
        return (combined, relativeBase);
    }

    private async Task<string> ResolveTargetFilePathAsync(
        string serverId,
        string volumeContainerPath,
        string subPath,
        CancellationToken cancellationToken)
    {
        var localVolumeRoot = await GetLocalVolumeRootAsync(serverId, volumeContainerPath, cancellationToken).ConfigureAwait(false);
        var normalizedSubPath = NormalizeSubPath(subPath);

        if (string.IsNullOrEmpty(normalizedSubPath))
        {
            throw new ArgumentException("A file path must be specified.", nameof(subPath));
        }

        var combined = Path.Combine(localVolumeRoot, normalizedSubPath.Replace('/', Path.DirectorySeparatorChar));
        ValidatePathSafety(localVolumeRoot, combined);
        return combined;
    }

    private async Task<string> GetLocalVolumeRootAsync(
        string serverId,
        string volumeContainerPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);
        ArgumentException.ThrowIfNullOrWhiteSpace(volumeContainerPath);

        var server = await gameServerRepository.GetByServerIdAsync(serverId).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"GameServer '{serverId}' was not found.");

        var normalizedContainerPath = volumeContainerPath.Trim();
        if (!normalizedContainerPath.StartsWith('/'))
        {
            normalizedContainerPath = "/" + normalizedContainerPath;
        }

        var volume = server.Volumes.FirstOrDefault(v => string.Equals(v.ContainerPath, normalizedContainerPath, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Volume for container path '{volumeContainerPath}' was not found on server '{serverId}'.");

        var config = await mountTypeConfigRepository.GetByKeyAsync(volume.MountType).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Mount type configuration '{volume.MountType}' was not found.");

        var localRoot = (config.GetOption("LocalPath") ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        if (string.IsNullOrEmpty(localRoot))
        {
            throw new NotSupportedException($"Mount type '{volume.MountType}' does not have a configured LocalPath for host filesystem access.");
        }

        var gameTypes = await gameTypeRepository.GetAllAsync(includeInactive: true).ConfigureAwait(false);
        var matchingType = gameTypes.FirstOrDefault(gt => gt.Revisions.Any(r => r.Id == server.GameTypeRevisionId));
        var gameTypeKey = matchingType?.Key ?? "default";

        var sourceToken = volume.ContainerPath.Trim('/').Replace('/', '-');
        var devicePathFormat = config.GetOption("DevicePathFormat") ?? "{gameTypeKey}/{serverId}/{Source}";
        var relativeDevicePath = devicePathFormat
            .Replace("{gameTypeKey}", gameTypeKey, StringComparison.OrdinalIgnoreCase)
            .Replace("{serverId}", serverId, StringComparison.OrdinalIgnoreCase)
            .Replace("{Source}", sourceToken, StringComparison.OrdinalIgnoreCase)
            .Trim('/');

        var fullVolumeRoot = Path.Combine(localRoot, relativeDevicePath.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(fullVolumeRoot))
        {
            Directory.CreateDirectory(fullVolumeRoot);
        }

        return fullVolumeRoot;
    }

    private static string NormalizeSubPath(string? subPath)
    {
        if (string.IsNullOrWhiteSpace(subPath))
        {
            return string.Empty;
        }

        return subPath.Replace('\\', '/').Trim('/');
    }

    private static void ValidatePathSafety(string root, string resolved)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullResolved = Path.GetFullPath(resolved);

        if (!fullResolved.Equals(fullRoot, StringComparison.OrdinalIgnoreCase)
            && !fullResolved.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !fullResolved.StartsWith(fullRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Access denied: Invalid relative path outside of volume root.");
        }
    }
}
