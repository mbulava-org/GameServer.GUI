using GameServer.API.Controllers.V2;
using GameServer.API.Dtos.V2;
using GameServer.API.Services.V2;
using Microsoft.AspNetCore.Http;
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

    #region List Tests

    [Fact]
    public async Task List_ReturnsBadRequest_WhenVolumePathMissing()
    {
        var result = await _controller.List("srv-1", "");
        Assert.IsType<BadRequestObjectResult>(result);

        var result2 = await _controller.List("srv-1", "   ");
        Assert.IsType<BadRequestObjectResult>(result2);
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
    public async Task List_HandlesExceptions()
    {
        _filesServiceMock.Setup(s => s.ListFilesAsync("srv-1", "keynotfound", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Missing key"));
        var r1 = await _controller.List("srv-1", "keynotfound");
        Assert.IsType<NotFoundObjectResult>(r1);

        _filesServiceMock.Setup(s => s.ListFilesAsync("srv-1", "dirnotfound", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DirectoryNotFoundException("Missing dir"));
        var r2 = await _controller.List("srv-1", "dirnotfound");
        Assert.IsType<NotFoundObjectResult>(r2);

        _filesServiceMock.Setup(s => s.ListFilesAsync("srv-1", "unauthorized", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Denied"));
        var r3 = await _controller.List("srv-1", "unauthorized");
        Assert.IsType<ForbidResult>(r3);

        _filesServiceMock.Setup(s => s.ListFilesAsync("srv-1", "notsupported", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotSupportedException("Not supported"));
        var r4 = await _controller.List("srv-1", "notsupported");
        var obj4 = Assert.IsType<ObjectResult>(r4);
        Assert.Equal(StatusCodes.Status400BadRequest, obj4.StatusCode);

        _filesServiceMock.Setup(s => s.ListFilesAsync("srv-1", "error", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Fatal"));
        var r5 = await _controller.List("srv-1", "error");
        var obj5 = Assert.IsType<ObjectResult>(r5);
        Assert.Equal(StatusCodes.Status500InternalServerError, obj5.StatusCode);
    }

    #endregion

    #region GetContent Tests

    [Fact]
    public async Task GetContent_ReturnsBadRequest_WhenParametersMissing()
    {
        var r1 = await _controller.GetContent("srv-1", "", "file.txt");
        Assert.IsType<BadRequestObjectResult>(r1);

        var r2 = await _controller.GetContent("srv-1", "/data", "");
        Assert.IsType<BadRequestObjectResult>(r2);
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
    public async Task GetContent_HandlesExceptions()
    {
        _filesServiceMock.Setup(s => s.GetFileContentTextAsync("srv-1", "/data", "filenotfound", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException("Missing file"));
        var r1 = await _controller.GetContent("srv-1", "/data", "filenotfound");
        Assert.IsType<NotFoundObjectResult>(r1);

        _filesServiceMock.Setup(s => s.GetFileContentTextAsync("srv-1", "/data", "keynotfound", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Missing key"));
        var r2 = await _controller.GetContent("srv-1", "/data", "keynotfound");
        Assert.IsType<NotFoundObjectResult>(r2);

        _filesServiceMock.Setup(s => s.GetFileContentTextAsync("srv-1", "/data", "unauth", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Denied"));
        var r3 = await _controller.GetContent("srv-1", "/data", "unauth");
        Assert.IsType<ForbidResult>(r3);

        _filesServiceMock.Setup(s => s.GetFileContentTextAsync("srv-1", "/data", "error", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Error"));
        var r4 = await _controller.GetContent("srv-1", "/data", "error");
        var obj4 = Assert.IsType<ObjectResult>(r4);
        Assert.Equal(StatusCodes.Status500InternalServerError, obj4.StatusCode);
    }

    #endregion

    #region Download Tests

    [Fact]
    public async Task Download_ReturnsBadRequest_WhenParametersMissing()
    {
        var r1 = await _controller.Download("srv-1", "", "file.txt");
        Assert.IsType<BadRequestObjectResult>(r1);

        var r2 = await _controller.Download("srv-1", "/data", "");
        Assert.IsType<BadRequestObjectResult>(r2);
    }

    [Fact]
    public async Task Download_ReturnsFile_WhenFound()
    {
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        _filesServiceMock.Setup(s => s.GetFileStreamAsync("srv-1", "/data", "test.bin", It.IsAny<CancellationToken>()))
            .ReturnsAsync((stream, "application/octet-stream", "test.bin"));

        var result = await _controller.Download("srv-1", "/data", "test.bin");
        var fileResult = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("application/octet-stream", fileResult.ContentType);
        Assert.Equal("test.bin", fileResult.FileDownloadName);
    }

    [Fact]
    public async Task Download_HandlesExceptions()
    {
        _filesServiceMock.Setup(s => s.GetFileStreamAsync("srv-1", "/data", "filenotfound", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException("Missing file"));
        var r1 = await _controller.Download("srv-1", "/data", "filenotfound");
        Assert.IsType<NotFoundObjectResult>(r1);

        _filesServiceMock.Setup(s => s.GetFileStreamAsync("srv-1", "/data", "keynotfound", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Missing key"));
        var r2 = await _controller.Download("srv-1", "/data", "keynotfound");
        Assert.IsType<NotFoundObjectResult>(r2);

        _filesServiceMock.Setup(s => s.GetFileStreamAsync("srv-1", "/data", "unauth", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Denied"));
        var r3 = await _controller.Download("srv-1", "/data", "unauth");
        Assert.IsType<ForbidResult>(r3);

        _filesServiceMock.Setup(s => s.GetFileStreamAsync("srv-1", "/data", "error", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Error"));
        var r4 = await _controller.Download("srv-1", "/data", "error");
        var obj4 = Assert.IsType<ObjectResult>(r4);
        Assert.Equal(StatusCodes.Status500InternalServerError, obj4.StatusCode);
    }

    #endregion

    #region SaveContent Tests

    [Fact]
    public async Task SaveContent_ReturnsBadRequest_WhenParametersMissing()
    {
        var r1 = await _controller.SaveContent("srv-1", "", "file.txt", new SaveFileContentRequestDto { Content = "data" });
        Assert.IsType<BadRequestObjectResult>(r1);

        var r2 = await _controller.SaveContent("srv-1", "/data", "", new SaveFileContentRequestDto { Content = "data" });
        Assert.IsType<BadRequestObjectResult>(r2);
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
    public async Task SaveContent_HandlesExceptions()
    {
        _filesServiceMock.Setup(s => s.SaveFileContentTextAsync("srv-1", "/data", "keynotfound", "data", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Missing key"));
        var r1 = await _controller.SaveContent("srv-1", "/data", "keynotfound", new SaveFileContentRequestDto { Content = "data" });
        Assert.IsType<NotFoundObjectResult>(r1);

        _filesServiceMock.Setup(s => s.SaveFileContentTextAsync("srv-1", "/data", "unauth", "data", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Denied"));
        var r2 = await _controller.SaveContent("srv-1", "/data", "unauth", new SaveFileContentRequestDto { Content = "data" });
        Assert.IsType<ForbidResult>(r2);

        _filesServiceMock.Setup(s => s.SaveFileContentTextAsync("srv-1", "/data", "error", "data", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Error"));
        var r3 = await _controller.SaveContent("srv-1", "/data", "error", new SaveFileContentRequestDto { Content = "data" });
        var obj3 = Assert.IsType<ObjectResult>(r3);
        Assert.Equal(StatusCodes.Status500InternalServerError, obj3.StatusCode);
    }

    #endregion

    #region Upload Tests

    [Fact]
    public async Task Upload_ReturnsBadRequest_WhenInvalid()
    {
        var r1 = await _controller.Upload("srv-1", "", null, null);
        Assert.IsType<BadRequestObjectResult>(r1);

        var r2 = await _controller.Upload("srv-1", "/data", null, null);
        Assert.IsType<BadRequestObjectResult>(r2);

        var emptyFileMock = new Mock<IFormFile>();
        emptyFileMock.Setup(f => f.Length).Returns(0);
        var r3 = await _controller.Upload("srv-1", "/data", null, emptyFileMock.Object);
        Assert.IsType<BadRequestObjectResult>(r3);
    }

    [Fact]
    public async Task Upload_ReturnsOk_WhenValid()
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(100);
        fileMock.Setup(f => f.FileName).Returns("uploaded.zip");
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));

        _filesServiceMock.Setup(s => s.UploadFileAsync("srv-1", "/data", null, It.IsAny<Stream>(), "uploaded.zip", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.Upload("srv-1", "/data", null, fileMock.Object);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Upload_HandlesExceptions()
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(100);
        fileMock.Setup(f => f.FileName).Returns("uploaded.zip");
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));

        _filesServiceMock.Setup(s => s.UploadFileAsync("srv-1", "keynotfound", null, It.IsAny<Stream>(), "uploaded.zip", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Missing key"));
        var r1 = await _controller.Upload("srv-1", "keynotfound", null, fileMock.Object);
        Assert.IsType<NotFoundObjectResult>(r1);

        _filesServiceMock.Setup(s => s.UploadFileAsync("srv-1", "unauth", null, It.IsAny<Stream>(), "uploaded.zip", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Denied"));
        var r2 = await _controller.Upload("srv-1", "unauth", null, fileMock.Object);
        Assert.IsType<ForbidResult>(r2);

        _filesServiceMock.Setup(s => s.UploadFileAsync("srv-1", "error", null, It.IsAny<Stream>(), "uploaded.zip", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Error"));
        var r3 = await _controller.Upload("srv-1", "error", null, fileMock.Object);
        var obj3 = Assert.IsType<ObjectResult>(r3);
        Assert.Equal(StatusCodes.Status500InternalServerError, obj3.StatusCode);
    }

    #endregion

    #region CreateDirectory Tests

    [Fact]
    public async Task CreateDirectory_ReturnsBadRequest_WhenParametersMissing()
    {
        var r1 = await _controller.CreateDirectory("srv-1", "", "newdir");
        Assert.IsType<BadRequestObjectResult>(r1);

        var r2 = await _controller.CreateDirectory("srv-1", "/data", "");
        Assert.IsType<BadRequestObjectResult>(r2);
    }

    [Fact]
    public async Task CreateDirectory_ReturnsOk()
    {
        _filesServiceMock.Setup(s => s.CreateDirectoryAsync("srv-1", "/data", "newdir", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.CreateDirectory("srv-1", "/data", "newdir");
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task CreateDirectory_HandlesExceptions()
    {
        _filesServiceMock.Setup(s => s.CreateDirectoryAsync("srv-1", "/data", "keynotfound", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Missing key"));
        var r1 = await _controller.CreateDirectory("srv-1", "/data", "keynotfound");
        Assert.IsType<NotFoundObjectResult>(r1);

        _filesServiceMock.Setup(s => s.CreateDirectoryAsync("srv-1", "/data", "unauth", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Denied"));
        var r2 = await _controller.CreateDirectory("srv-1", "/data", "unauth");
        Assert.IsType<ForbidResult>(r2);

        _filesServiceMock.Setup(s => s.CreateDirectoryAsync("srv-1", "/data", "error", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Error"));
        var r3 = await _controller.CreateDirectory("srv-1", "/data", "error");
        var obj3 = Assert.IsType<ObjectResult>(r3);
        Assert.Equal(StatusCodes.Status500InternalServerError, obj3.StatusCode);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task Delete_ReturnsBadRequest_WhenParametersMissing()
    {
        var r1 = await _controller.Delete("srv-1", "", "old.txt");
        Assert.IsType<BadRequestObjectResult>(r1);

        var r2 = await _controller.Delete("srv-1", "/data", "");
        Assert.IsType<BadRequestObjectResult>(r2);
    }

    [Fact]
    public async Task Delete_ReturnsOk()
    {
        _filesServiceMock.Setup(s => s.DeleteFileOrDirectoryAsync("srv-1", "/data", "/old.txt", false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.Delete("srv-1", "/data", "/old.txt", false);
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task Delete_HandlesExceptions()
    {
        _filesServiceMock.Setup(s => s.DeleteFileOrDirectoryAsync("srv-1", "/data", "filenotfound", false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException("Missing file"));
        var r1 = await _controller.Delete("srv-1", "/data", "filenotfound", false);
        Assert.IsType<NotFoundObjectResult>(r1);

        _filesServiceMock.Setup(s => s.DeleteFileOrDirectoryAsync("srv-1", "/data", "keynotfound", false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Missing key"));
        var r2 = await _controller.Delete("srv-1", "/data", "keynotfound", false);
        Assert.IsType<NotFoundObjectResult>(r2);

        _filesServiceMock.Setup(s => s.DeleteFileOrDirectoryAsync("srv-1", "/data", "unauth", false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Denied"));
        var r3 = await _controller.Delete("srv-1", "/data", "unauth", false);
        Assert.IsType<ForbidResult>(r3);

        _filesServiceMock.Setup(s => s.DeleteFileOrDirectoryAsync("srv-1", "/data", "error", false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Error"));
        var r4 = await _controller.Delete("srv-1", "/data", "error", false);
        var obj4 = Assert.IsType<ObjectResult>(r4);
        Assert.Equal(StatusCodes.Status500InternalServerError, obj4.StatusCode);
    }

    #endregion
}
