using System.IO.Compression;
using GameServer.Windows.Agent.Configurations;
using GameServer.Windows.Agent.Interfaces;
using Microsoft.Extensions.Options;

namespace GameServer.Windows.Agent.Services;

public sealed class FileManagerService : IFileManagerService
{
    private readonly ILogger<FileManagerService> _logger;
    private readonly StorageOptions _options;

    public FileManagerService(
        ILogger<FileManagerService> logger,
        IOptions<WindowsAgentOptions> options)
    {
        _logger = logger;
        _options = options.Value.Storage;
    }

    public IReadOnlyList<FileNode> ListFiles(string directoryPath, string? relativeSubPath = null)
    {
        var targetDir = string.IsNullOrWhiteSpace(relativeSubPath)
            ? Path.GetFullPath(directoryPath)
            : Path.GetFullPath(Path.Combine(directoryPath, relativeSubPath));

        if (!Directory.Exists(targetDir))
        {
            return [];
        }

        var dirInfo = new DirectoryInfo(targetDir);
        var nodes = new List<FileNode>();

        foreach (var dir in dirInfo.GetDirectories())
        {
            nodes.Add(new FileNode
            {
                Name = dir.Name,
                RelativePath = Path.GetRelativePath(directoryPath, dir.FullName),
                IsDirectory = true,
                SizeBytes = 0,
                LastModified = dir.LastWriteTimeUtc
            });
        }

        foreach (var file in dirInfo.GetFiles())
        {
            nodes.Add(new FileNode
            {
                Name = file.Name,
                RelativePath = Path.GetRelativePath(directoryPath, file.FullName),
                IsDirectory = false,
                SizeBytes = file.Length,
                LastModified = file.LastWriteTimeUtc
            });
        }

        return nodes.OrderByDescending(n => n.IsDirectory).ThenBy(n => n.Name).ToList();
    }

    public async Task<string> ReadTextFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        return await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteTextFileAsync(string filePath, string content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllTextAsync(filePath, content, cancellationToken).ConfigureAwait(false);
    }

    public async Task<BackupArchiveInfo> CreateBackupAsync(
        string serverId,
        string sourceDirectory,
        string? subDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);

        var targetSource = string.IsNullOrWhiteSpace(subDirectory)
            ? Path.GetFullPath(sourceDirectory)
            : Path.GetFullPath(Path.Combine(sourceDirectory, subDirectory));

        if (!Directory.Exists(targetSource))
        {
            throw new DirectoryNotFoundException($"Source directory for backup does not exist: {targetSource}");
        }

        var backupDir = Path.Combine(_options.BackupsDirectory, serverId);
        Directory.CreateDirectory(backupDir);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var backupId = $"{serverId}_{timestamp}";
        var zipFileName = $"{backupId}.zip";
        var zipFilePath = Path.Combine(backupDir, zipFileName);

        _logger.LogInformation("Creating backup archive '{ZipFile}' from '{Source}'", zipFilePath, targetSource);

        await Task.Run(() => ZipFile.CreateFromDirectory(targetSource, zipFilePath, CompressionLevel.Optimal, false), cancellationToken).ConfigureAwait(false);

        var fileInfo = new FileInfo(zipFilePath);
        return new BackupArchiveInfo
        {
            BackupId = backupId,
            FileName = zipFileName,
            SizeBytes = fileInfo.Length,
            CreatedAt = fileInfo.CreationTimeUtc
        };
    }

    public IReadOnlyList<BackupArchiveInfo> ListBackups(string serverId)
    {
        var backupDir = Path.Combine(_options.BackupsDirectory, serverId);
        if (!Directory.Exists(backupDir))
        {
            return [];
        }

        var dirInfo = new DirectoryInfo(backupDir);
        return dirInfo.GetFiles("*.zip")
            .OrderByDescending(f => f.CreationTimeUtc)
            .Select(f => new BackupArchiveInfo
            {
                BackupId = Path.GetFileNameWithoutExtension(f.Name),
                FileName = f.Name,
                SizeBytes = f.Length,
                CreatedAt = f.CreationTimeUtc
            })
            .ToList();
    }

    public async Task RestoreBackupAsync(string serverId, string backupId, string targetDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);

        var backupDir = Path.Combine(_options.BackupsDirectory, serverId);
        var zipFilePath = Path.Combine(backupDir, $"{backupId}.zip");

        if (!File.Exists(zipFilePath))
        {
            throw new FileNotFoundException($"Backup archive '{backupId}' not found at '{zipFilePath}'");
        }

        Directory.CreateDirectory(targetDirectory);
        _logger.LogInformation("Restoring backup archive '{ZipFile}' to '{Target}'", zipFilePath, targetDirectory);

        await Task.Run(() => ZipFile.ExtractToDirectory(zipFilePath, targetDirectory, overwriteFiles: true), cancellationToken).ConfigureAwait(false);
    }
}
