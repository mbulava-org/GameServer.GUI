namespace GameServer.Windows.Agent.Interfaces;

public class FileNode
{
    public string Name { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public long SizeBytes { get; set; }
    public DateTime LastModified { get; set; }
}

public class BackupArchiveInfo
{
    public string BackupId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public interface IFileManagerService
{
    IReadOnlyList<FileNode> ListFiles(string directoryPath, string? relativeSubPath = null);
    Task<string> ReadTextFileAsync(string filePath, CancellationToken cancellationToken = default);
    Task WriteTextFileAsync(string filePath, string content, CancellationToken cancellationToken = default);
    Task<BackupArchiveInfo> CreateBackupAsync(string serverId, string sourceDirectory, string? subDirectory = null, CancellationToken cancellationToken = default);
    IReadOnlyList<BackupArchiveInfo> ListBackups(string serverId);
    Task RestoreBackupAsync(string serverId, string backupId, string targetDirectory, CancellationToken cancellationToken = default);
}
