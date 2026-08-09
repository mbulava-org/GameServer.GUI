using Docker.DotNet;
using Docker.DotNet.Models;
using GameServer.Docker.Agent.Controllers;
using GameServer.Docker.Agent.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace GameServer.Docker.Agent.Tests.Controllers;

/// <summary>
/// Tests for ServicesController to ensure proper serialization of SwarmService objects.
/// Prevents regression of the critical bug where anonymous object mapping lost type information.
/// </summary>
public class ServicesControllerTests
{
    private readonly Mock<IDockerClient> _mockDockerClient;
    private readonly Mock<ILogger<ServicesController>> _mockLogger;
    private readonly Mock<ISwarmOperations> _mockSwarmOperations;

    public ServicesControllerTests()
    {
        _mockDockerClient = new Mock<IDockerClient>();
        _mockLogger = new Mock<ILogger<ServicesController>>();
        _mockSwarmOperations = new Mock<ISwarmOperations>();

        _mockDockerClient.Setup(x => x.Swarm).Returns(_mockSwarmOperations.Object);
    }

    private ServicesController CreateController()
    {
        return new ServicesController(_mockDockerClient.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ListServices_ShouldReturnFullSwarmServiceObjects_NotAnonymous()
    {
        // Arrange
        var controller = CreateController();

        var testServices = new List<SwarmService>
        {
            new()
            {
                ID = "service-1",
                Version = new global::Docker.DotNet.Models.Version { Index = 100 },
                Spec = new ServiceSpec
                {
                    Name = "minecraft-server",
                    Labels = new Dictionary<string, string>
                    {
                        ["gameserver.docker.managed"] = "true",
                        ["gameserver.docker.gametype"] = "minecraft"
                    },
                    TaskTemplate = new TaskSpec
                    {
                        ContainerSpec = new ContainerSpec
                        {
                            Image = "minecraft:latest",
                            Env = new List<string> { "EULA=true" }
                        }
                    }
                }
            }
        };

        _mockSwarmOperations.Setup(x => x.ListServicesAsync(It.IsAny<ServiceListParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testServices);

        // Act
        var actionResult = await controller.ListServices();

        // Assert - Get the OkObjectResult from ActionResult<T>
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<ServiceOperationResponse>(okResult.Value);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.True(response.Data.ContainsKey("services"));

        // CRITICAL TEST: Services must be returned as a collection (not mapped to anonymous)
        // Serialize and deserialize to verify SwarmService structure is preserved
        var json = JsonSerializer.Serialize(response);
        var jsonDoc = JsonDocument.Parse(json);

        // Extract services array from response (use PascalCase - System.Text.Json default)
        var servicesProp = jsonDoc.RootElement.GetProperty("Data").GetProperty("services");

        // Deserialize back to SwarmService - this is what Primary Service does
        var deserializedServices = JsonSerializer.Deserialize<List<SwarmService>>(servicesProp.GetRawText());

        Assert.NotNull(deserializedServices);
        Assert.Single(deserializedServices);

        var svc = deserializedServices[0];

        // CRITICAL: These properties must survive JSON round-trip!
        Assert.Equal("service-1", svc.ID);
        Assert.NotNull(svc.Spec);
        Assert.Equal("minecraft-server", svc.Spec.Name);
        Assert.NotNull(svc.Spec.Labels);
        Assert.Equal("true", svc.Spec.Labels["gameserver.docker.managed"]);
        Assert.NotNull(svc.Spec.TaskTemplate);
        Assert.NotNull(svc.Spec.TaskTemplate.ContainerSpec);
        Assert.Equal("minecraft:latest", svc.Spec.TaskTemplate.ContainerSpec.Image);
    }

    [Fact]
    public async Task InspectService_ShouldReturnFullSwarmServiceObject()
    {
        // Arrange
        var controller = CreateController();

        var testService = new SwarmService
        {
            ID = "service-123",
            Spec = new ServiceSpec
            {
                Name = "test-service",
                Labels = new Dictionary<string, string>
                {
                    ["key1"] = "value1"
                }
            }
        };

        _mockSwarmOperations.Setup(x => x.InspectServiceAsync("service-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testService);

        // Act
        var actionResult = await controller.InspectService("service-123");

        // Assert - Get the OkObjectResult from ActionResult<T>
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<ServiceOperationResponse>(okResult.Value);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.True(response.Data.ContainsKey("service"));

        // Serialize and deserialize to verify structure
        var json = JsonSerializer.Serialize(response);
        var jsonDoc = JsonDocument.Parse(json);

        var serviceProp = jsonDoc.RootElement.GetProperty("Data").GetProperty("service");
        var deserializedService = JsonSerializer.Deserialize<SwarmService>(serviceProp.GetRawText());

        Assert.NotNull(deserializedService);
        Assert.Equal("service-123", deserializedService.ID);
        Assert.NotNull(deserializedService.Spec);
        Assert.Equal("test-service", deserializedService.Spec.Name);
        Assert.NotNull(deserializedService.Spec.Labels);
        Assert.Equal("value1", deserializedService.Spec.Labels["key1"]);
    }

    [Fact]
    public async Task ListServices_WithLabelFilter_ShouldPassFilterToDockerClient()
    {
        // Arrange
        var controller = CreateController();
        
        _mockSwarmOperations.Setup(x => x.ListServicesAsync(
                It.Is<ServiceListParameters>(p =>
                    p.Filters != null &&
                    p.Filters.ContainsKey("label") &&
                    p.Filters["label"].ContainsKey("gameserver.docker.managed=true")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SwarmService>());

        // Act
        await controller.ListServices("gameserver.docker.managed=true");

        // Assert
        _mockSwarmOperations.Verify(
            x => x.ListServicesAsync(
                It.Is<ServiceListParameters>(p =>
                    p.Filters != null &&
                    p.Filters.ContainsKey("label") &&
                    p.Filters["label"].ContainsKey("gameserver.docker.managed=true")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
