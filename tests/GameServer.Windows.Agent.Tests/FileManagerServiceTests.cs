using GameServer.Windows.Agent.Configurations;
using GameServer.Windows.Agent.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GameServer.Windows.Agent.Tests;

public class FileManagerServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly FileManagerService _fileManager;

    public FileManagerServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"FileManagerTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);

        var options = Options.Create(new WindowsAgentOptions
        {
            Storage = new StorageOptions
            {
                BaseInstancesDirectory = Path.Combine(_tempDirectory, "instances"),
                BackupsDirectory = Path.Combine(_tempDirectory, "backups")
            }
        });

        _fileManager = new FileManagerService(NullLogger<FileManagerService>.Instance, options);
    }

    [Fact]
    public async Task WriteAndReadTextFileAsync_PreservesContent()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "config.ini");
        var content = "[ServerSettings]\nServerName=MyTestServer\nPort=7777";
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await _fileManager.WriteTextFileAsync(filePath, content, cancellationToken);
        var readContent = await _fileManager.ReadTextFileAsync(filePath, cancellationToken);

        // Assert
        Assert.Equal(content, readContent);
    }

    [Fact]
    public void ListFiles_ReturnsDirectoriesAndFiles()
    {
        // Arrange
        var subDir = Path.Combine(_tempDirectory, "subfolder");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(_tempDirectory, "file1.txt"), "hello");
        File.WriteAllText(Path.Combine(subDir, "file2.txt"), "world");

        // Act
        var rootList = _fileManager.ListFiles(_tempDirectory);

        // Assert
        Assert.Contains(rootList, n => n.Name == "subfolder" && n.IsDirectory);
        Assert.Contains(rootList, n => n.Name == "file1.txt" && !n.IsDirectory);
    }

    [Fact]
    public async Task CreateAndRestoreBackupAsync_CreatesZipAndRestoresFiles()
    {
        // Arrange
        var serverId = "test-server-1";
        var sourceDir = Path.Combine(_tempDirectory, "source");
        var restoreDir = Path.Combine(_tempDirectory, "restored");
        var cancellationToken = TestContext.Current.CancellationToken;

        Directory.CreateDirectory(sourceDir);
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "savegame.dat"), "save-data-12345", cancellationToken);

        // Act - Create Backup
        var backup = await _fileManager.CreateBackupAsync(serverId, sourceDir, null, cancellationToken);

        Assert.NotNull(backup);
        Assert.True(backup.SizeBytes > 0);
        Assert.True(File.Exists(Path.Combine(_tempDirectory, "backups", serverId, backup.FileName)));

        // Act - List Backups
        var backups = _fileManager.ListBackups(serverId);
        Assert.Single(backups);
        Assert.Equal(backup.BackupId, backups[0].BackupId);

        // Act - Restore Backup
        await _fileManager.RestoreBackupAsync(serverId, backup.BackupId, restoreDir, cancellationToken);

        // Assert
        var restoredFilePath = Path.Combine(restoreDir, "savegame.dat");
        Assert.True(File.Exists(restoredFilePath));
        var restoredContent = await File.ReadAllTextAsync(restoredFilePath, cancellationToken);
        Assert.Equal("save-data-12345", restoredContent);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch
        {
            // Cleanup best effort
        }
    }
}
