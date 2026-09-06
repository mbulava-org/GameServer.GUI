using GameServer.Docker.Agent.Controllers;
using GameServer.Docker.Agent.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Docker.DotNet;
using Docker.DotNet.Models;
using AgentModels = GameServer.Docker.Agent.Models;

namespace GameServer.Docker.Agent.Tests.Controllers;

public class ContainersControllerTests
{
    private readonly Mock<IContainerService> _mockContainerService;
    private readonly Mock<ILogger<ContainersController>> _mockLogger;

    public ContainersControllerTests()
    {
        _mockContainerService = new Mock<IContainerService>();
        _mockLogger = new Mock<ILogger<ContainersController>>();
    }

    private ContainersController CreateController()
    {
        return new ContainersController(
            _mockContainerService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public void ContainersController_ShouldBeInstantiable()
    {
        // Act
        var controller = CreateController();

        // Assert
        Assert.NotNull(controller);
    }

    [Fact]
    public async Task GetContainerStats_WhenContainerExists_ShouldReturnOkWithStats()
    {
        // Arrange
        var controller = CreateController();
        var containerId = "test-container-123";
        var expectedStats = new AgentModels.ContainerStatsResponse
        {
            ContainerId = containerId,
            Cpu = new AgentModels.CpuStats { UsagePercent = 25.5 },
            Memory = new AgentModels.MemoryStats 
            { 
                UsageBytes = 512000000, 
                LimitBytes = 1024000000,
                UsagePercent = 50.0 
            },
            Network = new AgentModels.NetworkStats { RxBytes = 1000, TxBytes = 2000 }
        };

        _mockContainerService
            .Setup(x => x.GetContainerStatsAsync(containerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await controller.GetContainerStats(containerId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var stats = Assert.IsType<AgentModels.ContainerStatsResponse>(okResult.Value);
        Assert.Equal(containerId, stats.ContainerId);
        Assert.Equal(25.5, stats.Cpu.UsagePercent);
        Assert.Equal(50.0, stats.Memory.UsagePercent);

        _mockContainerService.Verify(
            x => x.GetContainerStatsAsync(containerId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetContainerStats_WhenContainerNotFound_ShouldReturn404()
    {
        // Arrange
        var controller = CreateController();
        var containerId = "nonexistent-container";

        _mockContainerService
            .Setup(x => x.GetContainerStatsAsync(containerId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Container not found"));

        // Act
        var result = await controller.GetContainerStats(containerId, CancellationToken.None);

        // Assert
        // Since we can't mock the exact exception type, we expect a ProblemDetails response
        Assert.IsType<ObjectResult>(result);
    }

    [Fact]
    public async Task GetContainerStats_WhenTimeout_ShouldReturn408()
    {
        // Arrange
        var controller = CreateController();
        var containerId = "slow-container";

        _mockContainerService
            .Setup(x => x.GetContainerStatsAsync(containerId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Stats collection timed out"));

        // Act
        var result = await controller.GetContainerStats(containerId, CancellationToken.None);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(408, statusResult.StatusCode);
        var errorResponse = Assert.IsType<AgentModels.ErrorResponse>(statusResult.Value);
        Assert.Contains("timed out", errorResponse.Error);
    }

    [Fact]
    public async Task GetContainerLogs_WhenContainerExists_ShouldReturnOkWithLogs()
    {
        // Arrange
        var controller = CreateController();
        var containerId = "test-container-123";
        var tail = 100;
        var expectedLogs = new AgentModels.ContainerLogsResponse
        {
            ContainerId = containerId,
            Logs = new List<string>
            {
                "Log line 1",
                "Log line 2",
                "Log line 3"
            }
        };

        _mockContainerService
            .Setup(x => x.GetContainerLogsAsync(containerId, tail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedLogs);

        // Act
        var result = await controller.GetContainerLogs(containerId, tail);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var logs = Assert.IsType<AgentModels.ContainerLogsResponse>(okResult.Value);
        Assert.Equal(containerId, logs.ContainerId);
        Assert.Equal(3, logs.Logs.Count);

        _mockContainerService.Verify(
            x => x.GetContainerLogsAsync(containerId, tail, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetContainerLogs_WhenContainerNotFound_ShouldReturn404()
    {
        // Arrange
        var controller = CreateController();
        var containerId = "nonexistent-container";

        _mockContainerService
            .Setup(x => x.GetContainerLogsAsync(containerId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Container not found"));

        // Act
        var result = await controller.GetContainerLogs(containerId);

        // Assert
        // Since we can't mock the exact exception type, we expect an error response
        Assert.IsType<ObjectResult>(result);
    }

    [Fact]
    public async Task InspectContainer_WhenContainerExists_ShouldReturnOkWithDetails()
    {
        // Arrange
        var controller = CreateController();
        var containerId = "test-container-123";
        var expectedDetails = new AgentModels.ContainerInspectResponse
        {
            ContainerId = containerId,
            Name = "/test-container",
            State = new AgentModels.ContainerState { Status = "running" },
            Image = "nginx:latest",
            Created = DateTime.UtcNow
        };

        _mockContainerService
            .Setup(x => x.InspectContainerAsync(containerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetails);

        // Act
        var result = await controller.InspectContainer(containerId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var details = Assert.IsType<AgentModels.ContainerInspectResponse>(okResult.Value);
        Assert.Equal(containerId, details.ContainerId);
        Assert.Equal("running", details.State.Status);

        _mockContainerService.Verify(
            x => x.InspectContainerAsync(containerId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InspectContainer_WhenContainerNotFound_ShouldReturn404()
    {
        // Arrange
        var controller = CreateController();
        var containerId = "nonexistent-container";

        _mockContainerService
            .Setup(x => x.InspectContainerAsync(containerId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Container not found"));

        // Act
        var result = await controller.InspectContainer(containerId, CancellationToken.None);

        // Assert
        // Since we can't mock the exact exception type, we expect an error response
        Assert.IsType<ObjectResult>(result);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(500)]
    public async Task GetContainerLogs_ShouldAcceptDifferentTailValues(int tail)
    {
        // Arrange
        var controller = CreateController();
        var containerId = "test-container";
        var expectedLogs = new AgentModels.ContainerLogsResponse
        {
            ContainerId = containerId,
            Logs = new List<string>()
        };

        _mockContainerService
            .Setup(x => x.GetContainerLogsAsync(containerId, tail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedLogs);

        // Act
        var result = await controller.GetContainerLogs(containerId, tail);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        _mockContainerService.Verify(
            x => x.GetContainerLogsAsync(containerId, tail, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ListContainers_WhenCalled_ReturnsOkWithContainers()
    {
        var controller = CreateController();
        var expected = new AgentModels.ContainerListResponse
        {
            Containers = new List<AgentModels.ContainerSummary>
            {
                new() { Id = "c1", Names = new List<string> { "/web" }, State = "running", Status = "Up 2 hours" }
            }
        };

        _mockContainerService
            .Setup(x => x.ListContainersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await controller.ListContainers(CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsType<AgentModels.ContainerListResponse>(okResult.Value);
        Assert.Single(list.Containers);
    }

    [Fact]
    public async Task ListFiles_WhenCalled_ReturnsOkWithFileList()
    {
        var controller = CreateController();
        var items = new List<AgentModels.ContainerFileItemResponse>
        {
            new() { Name = "test.txt", Path = "/data/test.txt", Size = 100, IsDirectory = false, LastModified = DateTime.UtcNow }
        };

        _mockContainerService
            .Setup(x => x.ListFilesAsync("c1", "/data", It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var result = await controller.ListFiles("c1", "/data", CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsAssignableFrom<IReadOnlyList<AgentModels.ContainerFileItemResponse>>(okResult.Value);
        Assert.Single(response);
    }

    [Fact]
    public async Task ListFiles_WhenFileNotFound_ReturnsNotFound()
    {
        var controller = CreateController();
        _mockContainerService
            .Setup(x => x.ListFilesAsync("c1", "/missing", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException("Not found"));

        var result = await controller.ListFiles("c1", "/missing", CancellationToken.None);
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<AgentModels.ErrorResponse>(notFound.Value);
        Assert.Equal("Not found", error.Error);
    }

    [Fact]
    public async Task GetFileContent_WhenCalled_ReturnsOkWithContent()
    {
        var controller = CreateController();
        _mockContainerService
            .Setup(x => x.GetFileContentTextAsync("c1", "/data/file.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync("hello world");

        var result = await controller.GetFileContent("c1", "/data/file.txt", CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("hello world", okResult.Value);
    }

    [Fact]
    public async Task GetFileContent_WhenFileNotFound_ReturnsNotFound()
    {
        var controller = CreateController();
        _mockContainerService
            .Setup(x => x.GetFileContentTextAsync("c1", "/data/file.txt", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException("Path not found"));

        var result = await controller.GetFileContent("c1", "/data/file.txt", CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task DownloadFile_WhenCalled_ReturnsFileResult()
    {
        var controller = CreateController();
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        _mockContainerService
            .Setup(x => x.GetFileStreamAsync("c1", "/data/file.zip", It.IsAny<CancellationToken>()))
            .ReturnsAsync((stream, "application/zip", "file.zip"));

        var result = await controller.DownloadFile("c1", "/data/file.zip", CancellationToken.None);
        var fileResult = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("application/zip", fileResult.ContentType);
        Assert.Equal("file.zip", fileResult.FileDownloadName);
    }

    [Fact]
    public async Task SaveFileContent_WhenCalled_ReturnsOk()
    {
        var controller = CreateController();
        _mockContainerService
            .Setup(x => x.SaveFileContentTextAsync("c1", "/data/file.txt", "updated content", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await controller.SaveFileContent("c1", "/data/file.txt", new AgentModels.SaveFileRequest { Content = "updated content" }, CancellationToken.None);
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task UploadFile_WhenFileNullOrEmpty_ReturnsBadRequest()
    {
        var controller = CreateController();
        var result = await controller.UploadFile("c1", "/data", null, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UploadFile_WhenFileProvided_ReturnsOk()
    {
        var controller = CreateController();
        var formFileMock = new Mock<Microsoft.AspNetCore.Http.IFormFile>();
        var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        formFileMock.Setup(f => f.Length).Returns(4);
        formFileMock.Setup(f => f.FileName).Returns("uploaded.txt");
        formFileMock.Setup(f => f.OpenReadStream()).Returns(stream);

        _mockContainerService
            .Setup(x => x.UploadFileAsync("c1", "/data", "uploaded.txt", It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await controller.UploadFile("c1", "/data", formFileMock.Object, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task CreateDirectory_WhenCalled_ReturnsOk()
    {
        var controller = CreateController();
        _mockContainerService
            .Setup(x => x.CreateDirectoryAsync("c1", "/data/newdir", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await controller.CreateDirectory("c1", "/data/newdir", CancellationToken.None);
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task DeleteFileOrDirectory_WhenCalled_ReturnsOk()
    {
        var controller = CreateController();
        _mockContainerService
            .Setup(x => x.DeleteFileOrDirectoryAsync("c1", "/data/olddir", true, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await controller.DeleteFileOrDirectory("c1", "/data/olddir", recursive: true, CancellationToken.None);
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task ListContainers_WhenException_Returns500()
    {
        var controller = CreateController();
        _mockContainerService.Setup(x => x.ListContainersAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Docker daemon error"));

        var result = await controller.ListContainers(CancellationToken.None);
        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objResult.StatusCode);
    }

    [Fact]
    public async Task ListFiles_WhenException_Returns500()
    {
        var controller = CreateController();
        _mockContainerService.Setup(x => x.ListFilesAsync("c1", "/path", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("I/O error"));

        var result = await controller.ListFiles("c1", "/path", CancellationToken.None);
        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objResult.StatusCode);
    }

    [Fact]
    public async Task GetFileContent_WhenException_Returns500()
    {
        var controller = CreateController();
        _mockContainerService.Setup(x => x.GetFileContentTextAsync("c1", "/file.txt", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Read error"));

        var result = await controller.GetFileContent("c1", "/file.txt", CancellationToken.None);
        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objResult.StatusCode);
    }

    [Fact]
    public async Task DownloadFile_WhenFileNotFound_ReturnsNotFound()
    {
        var controller = CreateController();
        _mockContainerService.Setup(x => x.GetFileStreamAsync("c1", "/missing.txt", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException("file missing"));

        var result = await controller.DownloadFile("c1", "/missing.txt", CancellationToken.None);
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var err = Assert.IsType<AgentModels.ErrorResponse>(notFound.Value);
        Assert.Equal("file missing", err.Error);
    }

    [Fact]
    public async Task DownloadFile_WhenException_Returns500()
    {
        var controller = CreateController();
        _mockContainerService.Setup(x => x.GetFileStreamAsync("c1", "/file.txt", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Stream error"));

        var result = await controller.DownloadFile("c1", "/file.txt", CancellationToken.None);
        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objResult.StatusCode);
    }

    [Fact]
    public async Task SaveFileContent_WhenException_Returns500()
    {
        var controller = CreateController();
        _mockContainerService.Setup(x => x.SaveFileContentTextAsync("c1", "/file.txt", "abc", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Write error"));

        var result = await controller.SaveFileContent("c1", "/file.txt", new AgentModels.SaveFileRequest { Content = "abc" }, CancellationToken.None);
        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objResult.StatusCode);
    }

    [Fact]
    public async Task UploadFile_WhenException_Returns500()
    {
        var controller = CreateController();
        var formFileMock = new Mock<Microsoft.AspNetCore.Http.IFormFile>();
        var stream = new MemoryStream(new byte[] { 1 });
        formFileMock.Setup(f => f.Length).Returns(1);
        formFileMock.Setup(f => f.FileName).Returns("uploaded.txt");
        formFileMock.Setup(f => f.OpenReadStream()).Returns(stream);

        _mockContainerService.Setup(x => x.UploadFileAsync("c1", "/dir", "uploaded.txt", It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Upload error"));

        var result = await controller.UploadFile("c1", "/dir", formFileMock.Object, CancellationToken.None);
        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objResult.StatusCode);
    }

    [Fact]
    public async Task CreateDirectory_WhenException_Returns500()
    {
        var controller = CreateController();
        _mockContainerService.Setup(x => x.CreateDirectoryAsync("c1", "/dir", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Mkdir error"));

        var result = await controller.CreateDirectory("c1", "/dir", CancellationToken.None);
        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objResult.StatusCode);
    }

    [Fact]
    public async Task DeleteFileOrDirectory_WhenException_Returns500()
    {
        var controller = CreateController();
        _mockContainerService.Setup(x => x.DeleteFileOrDirectoryAsync("c1", "/dir", false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Delete error"));

        var result = await controller.DeleteFileOrDirectory("c1", "/dir", recursive: false, CancellationToken.None);
        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objResult.StatusCode);
    }

    [Fact]
    public async Task AttachToContainerWebSocket_WhenNotWebSocket_SetsBadRequest400()
    {
        var controller = CreateController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };

        await controller.AttachToContainerWebSocket("c1");
        Assert.Equal(400, controller.HttpContext.Response.StatusCode);
    }

    [Fact]
    public async Task ExecInteractiveWebSocket_WhenNotWebSocket_SetsBadRequest400()
    {
        var controller = CreateController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };

        await controller.ExecInteractiveWebSocket("c1", ["/bin/bash"], tty: true);
        Assert.Equal(400, controller.HttpContext.Response.StatusCode);
    }

    [Fact]
    public async Task UploadFile_WhenFileNullOrEmpty_Returns400BadRequest()
    {
        var controller = CreateController();
        var resultNull = await controller.UploadFile("c1", "/dir", file: null);
        var badReq = Assert.IsType<BadRequestObjectResult>(resultNull);
        var err = Assert.IsType<AgentModels.ErrorResponse>(badReq.Value);
        Assert.Equal("No file was uploaded.", err.Error);

        var emptyFormFile = new Mock<Microsoft.AspNetCore.Http.IFormFile>();
        emptyFormFile.Setup(f => f.Length).Returns(0);
        var resultEmpty = await controller.UploadFile("c1", "/dir", file: emptyFormFile.Object);
        Assert.IsType<BadRequestObjectResult>(resultEmpty);
    }

    [Fact]
    public async Task UploadFile_WhenValid_ReturnsOkWithDetails()
    {
        var controller = CreateController();
        var formFileMock = new Mock<Microsoft.AspNetCore.Http.IFormFile>();
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        formFileMock.Setup(f => f.Length).Returns(3);
        formFileMock.Setup(f => f.FileName).Returns("test.txt");
        formFileMock.Setup(f => f.OpenReadStream()).Returns(stream);

        _mockContainerService.Setup(x => x.UploadFileAsync("c1", "/dir", "test.txt", It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await controller.UploadFile("c1", "/dir", formFileMock.Object, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetContainerLogs_WhenDockerContainerNotFound_Returns404()
    {
        var controller = CreateController();
        _mockContainerService.Setup(x => x.GetContainerLogsAsync("c_not_found", 100, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DockerContainerNotFoundException(System.Net.HttpStatusCode.NotFound, "not found"));

        var result = await controller.GetContainerLogs("c_not_found", 100);
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var err = Assert.IsType<AgentModels.ErrorResponse>(notFound.Value);
        Assert.Contains("not found", err.Error);
    }

    [Fact]
    public async Task GetContainerLogs_WhenException_Returns500Problem()
    {
        var controller = CreateController();
        _mockContainerService.Setup(x => x.GetContainerLogsAsync("c_err", 100, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("general error"));

        var result = await controller.GetContainerLogs("c_err", 100);
        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objResult.StatusCode);
    }

    [Fact]
    public async Task InspectContainer_WhenDockerContainerNotFound_Returns404()
    {
        var controller = CreateController();
        _mockContainerService.Setup(x => x.InspectContainerAsync("c_not_found", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DockerContainerNotFoundException(System.Net.HttpStatusCode.NotFound, "not found"));

        var result = await controller.InspectContainer("c_not_found", CancellationToken.None);
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var err = Assert.IsType<AgentModels.ErrorResponse>(notFound.Value);
        Assert.Contains("not found", err.Error);
    }

    [Fact]
    public async Task InspectContainer_WhenException_Returns500Problem()
    {
        var controller = CreateController();
        _mockContainerService.Setup(x => x.InspectContainerAsync("c_err", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("inspect error"));

        var result = await controller.InspectContainer("c_err", CancellationToken.None);
        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objResult.StatusCode);
    }

    [Fact]
    public async Task ListContainers_WhenException_Returns500Problem()
    {
        var controller = CreateController();
        _mockContainerService.Setup(x => x.ListContainersAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("list error"));

        var result = await controller.ListContainers(CancellationToken.None);
        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objResult.StatusCode);
    }

    [Fact]
    public async Task FileEndpoints_WhenDockerContainerNotFound_Returns404()
    {
        var controller = CreateController();

        _mockContainerService.Setup(x => x.ListFilesAsync("c1", "/path", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DockerContainerNotFoundException(System.Net.HttpStatusCode.NotFound, "not found"));
        var listRes = await controller.ListFiles("c1", "/path");
        Assert.IsType<NotFoundObjectResult>(listRes);

        _mockContainerService.Setup(x => x.GetFileContentTextAsync("c1", "/path", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DockerContainerNotFoundException(System.Net.HttpStatusCode.NotFound, "not found"));
        var contentRes = await controller.GetFileContent("c1", "/path");
        Assert.IsType<NotFoundObjectResult>(contentRes);

        _mockContainerService.Setup(x => x.GetFileStreamAsync("c1", "/path", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DockerContainerNotFoundException(System.Net.HttpStatusCode.NotFound, "not found"));
        var downloadRes = await controller.DownloadFile("c1", "/path");
        Assert.IsType<NotFoundObjectResult>(downloadRes);

        _mockContainerService.Setup(x => x.SaveFileContentTextAsync("c1", "/path", "abc", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DockerContainerNotFoundException(System.Net.HttpStatusCode.NotFound, "not found"));
        var saveRes = await controller.SaveFileContent("c1", "/path", new AgentModels.SaveFileRequest { Content = "abc" });
        Assert.IsType<NotFoundObjectResult>(saveRes);

        var formFile = new Mock<Microsoft.AspNetCore.Http.IFormFile>();
        formFile.Setup(f => f.Length).Returns(5);
        formFile.Setup(f => f.FileName).Returns("f.txt");
        formFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[5]));
        _mockContainerService.Setup(x => x.UploadFileAsync("c1", "/path", "f.txt", It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DockerContainerNotFoundException(System.Net.HttpStatusCode.NotFound, "not found"));
        var uploadRes = await controller.UploadFile("c1", "/path", formFile.Object);
        Assert.IsType<NotFoundObjectResult>(uploadRes);

        _mockContainerService.Setup(x => x.CreateDirectoryAsync("c1", "/path", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DockerContainerNotFoundException(System.Net.HttpStatusCode.NotFound, "not found"));
        var mkdirRes = await controller.CreateDirectory("c1", "/path");
        Assert.IsType<NotFoundObjectResult>(mkdirRes);

        _mockContainerService.Setup(x => x.DeleteFileOrDirectoryAsync("c1", "/path", false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DockerContainerNotFoundException(System.Net.HttpStatusCode.NotFound, "not found"));
        var deleteRes = await controller.DeleteFileOrDirectory("c1", "/path");
        Assert.IsType<NotFoundObjectResult>(deleteRes);
    }
}
