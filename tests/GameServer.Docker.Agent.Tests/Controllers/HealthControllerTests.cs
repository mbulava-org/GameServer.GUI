using Docker.DotNet;
using GameServer.Docker.Agent.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using AgentModels = GameServer.Docker.Agent.Models;

namespace GameServer.Docker.Agent.Tests.Controllers;

public class HealthControllerTests
{
    private readonly Mock<ILogger<HealthController>> _mockLogger;
    private readonly Mock<IDockerClient> _mockDockerClient;

    public HealthControllerTests()
    {
        _mockLogger = new Mock<ILogger<HealthController>>();
        _mockDockerClient = new Mock<IDockerClient>();
    }

    private HealthController CreateController()
    {
        return new HealthController(
            _mockLogger.Object,
            _mockDockerClient.Object
        );
    }

    [Fact]
    public void HealthController_ShouldBeInstantiable()
    {
        // Act
        var controller = CreateController();

        // Assert
        Assert.NotNull(controller);
    }

    [Fact]
    public void GetHealth_ShouldReturnHealthyStatus()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = controller.GetHealth();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var healthResponse = Assert.IsType<AgentModels.HealthResponse>(okResult.Value);
        Assert.Equal("healthy", healthResponse.Status);
    }

    [Fact]
    public void GetHealth_ShouldIncludeTimestamp()
    {
        // Arrange
        var controller = CreateController();
        var beforeCall = DateTime.UtcNow;

        // Act
        var result = controller.GetHealth();
        var afterCall = DateTime.UtcNow;

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var healthResponse = Assert.IsType<AgentModels.HealthResponse>(okResult.Value);
        Assert.InRange(healthResponse.Timestamp, beforeCall, afterCall);
    }

    [Fact]
    public void GetHealth_ShouldIncludeNodeName()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = controller.GetHealth();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var healthResponse = Assert.IsType<AgentModels.HealthResponse>(okResult.Value);
        Assert.NotNull(healthResponse.NodeName);
        Assert.NotEmpty(healthResponse.NodeName);
    }

    [Fact]
    public void GetHealth_ShouldIncludeVersion()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = controller.GetHealth();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var healthResponse = Assert.IsType<AgentModels.HealthResponse>(okResult.Value);
        Assert.NotNull(healthResponse.Version);
        Assert.NotEmpty(healthResponse.Version);
    }

    [Fact]
    public void GetHealth_ShouldReturnOkStatusCode()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = controller.GetHealth();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact]
    public void GetHealth_ShouldBeCallableMultipleTimes()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result1 = controller.GetHealth();
        var result2 = controller.GetHealth();
        var result3 = controller.GetHealth();

        // Assert
        Assert.IsType<OkObjectResult>(result1);
        Assert.IsType<OkObjectResult>(result2);
        Assert.IsType<OkObjectResult>(result3);

        var okResult1 = (OkObjectResult)result1;
        var okResult2 = (OkObjectResult)result2;
        var okResult3 = (OkObjectResult)result3;

        var health1 = Assert.IsType<AgentModels.HealthResponse>(okResult1.Value);
        var health2 = Assert.IsType<AgentModels.HealthResponse>(okResult2.Value);
        var health3 = Assert.IsType<AgentModels.HealthResponse>(okResult3.Value);

        Assert.Equal("healthy", health1.Status);
        Assert.Equal("healthy", health2.Status);
        Assert.Equal("healthy", health3.Status);
    }

    [Fact]
    public void GetHealth_WhenNodeNameEnvironmentVariableSet_ShouldUseIt()
    {
        // Arrange
        var testNodeName = "test-node-123";
        Environment.SetEnvironmentVariable("NODE_NAME", testNodeName);
        var controller = CreateController();

        try
        {
            // Act
            var result = controller.GetHealth();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var healthResponse = Assert.IsType<AgentModels.HealthResponse>(okResult.Value);
            Assert.Equal(testNodeName, healthResponse.NodeName);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("NODE_NAME", null);
        }
    }
}
