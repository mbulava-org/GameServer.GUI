using GameServer.Windows.Agent.Controllers;
using GameServer.Windows.Agent.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace GameServer.Windows.Agent.Tests.Controllers;

public class FilesControllerTests
{
    private readonly Mock<IFileManagerService> _fileManagerMock;
    private readonly Mock<ILogger<FilesController>> _loggerMock;
    private readonly FilesController _controller;

    public FilesControllerTests()
    {
        _fileManagerMock = new Mock<IFileManagerService>();
        _loggerMock = new Mock<ILogger<FilesController>>();
        _controller = new FilesController(_fileManagerMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void ListFiles_WhenDirectoryPathEmpty_ReturnsBadRequest()
    {
        // Act
        var result = _controller.ListFiles(string.Empty);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void ListFiles_WhenValid_ReturnsFiles()
    {
        // Arrange
        var files = new List<FileNode>
        {
            new() { Name = "ServerSettings.ini", IsDirectory = false, SizeBytes = 2048 },
            new() { Name = "Config", IsDirectory = true }
        };
        _fileManagerMock.Setup(m => m.ListFiles(@"C:\GameServers\conan-01", null)).Returns(files);

        // Act
        var result = _controller.ListFiles(@"C:\GameServers\conan-01");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<FileNode>>(okResult.Value);
        Assert.Equal(2, returned.Count);
    }

    [Fact]
    public async Task ReadFile_WhenExists_ReturnsContent()
    {
        // Arrange
        var filePath = @"C:\GameServers\conan-01\ServerSettings.ini";
        _fileManagerMock.Setup(m => m.ReadTextFileAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync("[ServerSettings]\nAdminPassword=secret\n");

        // Act
        var result = await _controller.ReadFile(filePath, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("[ServerSettings]\nAdminPassword=secret\n", okResult.Value);
    }

    [Fact]
    public async Task ReadFile_WhenFileNotFound_ReturnsNotFound()
    {
        // Arrange
        _fileManagerMock.Setup(m => m.ReadTextFileAsync(@"C:\missing.ini", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException());

        // Act
        var result = await _controller.ReadFile(@"C:\missing.ini", CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task WriteFile_WhenValid_ReturnsOk()
    {
        // Arrange
        var filePath = @"C:\GameServers\conan-01\ServerSettings.ini";
        var request = new FilesController.FileContentRequest { Content = "NewContent" };
        _fileManagerMock.Setup(m => m.WriteTextFileAsync(filePath, "NewContent", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.WriteFile(filePath, request, CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task CreateBackup_WhenValid_ReturnsBackupInfo()
    {
        // Arrange
        var backup = new BackupArchiveInfo
        {
            BackupId = "conan-backup-1",
            SizeBytes = 1048576,
            FileName = "backup-2026.zip"
        };
        _fileManagerMock.Setup(m => m.CreateBackupAsync("conan-01", @"C:\GameServers\conan-01", "Saved", It.IsAny<CancellationToken>()))
            .ReturnsAsync(backup);

        // Act
        var result = await _controller.CreateBackup("conan-01", @"C:\GameServers\conan-01", "Saved", CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var info = Assert.IsType<BackupArchiveInfo>(okResult.Value);
        Assert.Equal("conan-backup-1", info.BackupId);
    }

    [Fact]
    public void ListBackups_ReturnsBackups()
    {
        // Arrange
        var backups = new List<BackupArchiveInfo>
        {
            new() { BackupId = "b1", FileName = "b1.zip" },
            new() { BackupId = "b2", FileName = "b2.zip" }
        };
        _fileManagerMock.Setup(m => m.ListBackups("conan-01")).Returns(backups);

        // Act
        var result = _controller.ListBackups("conan-01");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<BackupArchiveInfo>>(okResult.Value);
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task RestoreBackup_WhenValid_ReturnsOk()
    {
        // Arrange
        _fileManagerMock.Setup(m => m.RestoreBackupAsync("conan-01", "b1", @"C:\GameServers\conan-01", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.RestoreBackup("conan-01", "b1", @"C:\GameServers\conan-01", CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }
}
