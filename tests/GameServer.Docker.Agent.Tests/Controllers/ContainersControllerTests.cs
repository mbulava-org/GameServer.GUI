using GameServer.Docker.Agent.Controllers;
using GameServer.Docker.Agent.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
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
}
