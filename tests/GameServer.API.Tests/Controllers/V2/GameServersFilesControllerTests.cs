using GameServer.API.Controllers.V2;
using GameServer.API.Dtos.V2;
using GameServer.API.Services.V2;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameServer.API.Tests.Controllers.V2;

public class GameServersFilesControllerTests
{
    private readonly Mock<IGameServerFilesService> _filesServiceMock;
    private readonly GameServersFilesController _controller;

    public GameServersFilesControllerTests()
    {
        _filesServiceMock = new Mock<IGameServerFilesService>();
        _controller = new GameServersFilesController(_filesServiceMock.Object, NullLogger<GameServersFilesController>.Instance);
    }

    [Fact]
    public async Task List_ReturnsBadRequest_WhenVolumePathMissing()
    {
        var result = await _controller.List("srv-1", "");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task List_ReturnsOk_WithFiles()
    {
        var expectedFiles = new List<FileItemDto>
        {
            new() { Name = "test.txt", Path = "/test.txt", Size = 100, IsDirectory = false, LastModified = DateTime.UtcNow }
        };

        _filesServiceMock.Setup(s => s.ListFilesAsync("srv-1", "/data", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedFiles);

        var result = await _controller.List("srv-1", "/data");
        var okResult = Assert.IsType<OkObjectResult>(result);
        var files = Assert.IsAssignableFrom<IReadOnlyList<FileItemDto>>(okResult.Value);
        Assert.Single(files);
        Assert.Equal("test.txt", files[0].Name);
    }

    [Fact]
    public async Task GetContent_ReturnsContent_WhenFound()
    {
        _filesServiceMock.Setup(s => s.GetFileContentTextAsync("srv-1", "/data", "/server.properties", It.IsAny<CancellationToken>()))
            .ReturnsAsync("motd=test");

        var result = await _controller.GetContent("srv-1", "/data", "/server.properties");
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("motd=test", okResult.Value);
    }

    [Fact]
    public async Task SaveContent_ReturnsOk()
    {
        _filesServiceMock.Setup(s => s.SaveFileContentTextAsync("srv-1", "/data", "/server.properties", "new content", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.SaveContent("srv-1", "/data", "/server.properties", new SaveFileContentRequestDto { Content = "new content" });
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsOk()
    {
        _filesServiceMock.Setup(s => s.DeleteFileOrDirectoryAsync("srv-1", "/data", "/old.txt", false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.Delete("srv-1", "/data", "/old.txt", false);
        Assert.IsType<OkResult>(result);
    }
}
