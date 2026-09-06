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
    public async Task CreateService_WhenSuccessful_ReturnsOkWithServiceId()
    {
        var controller = CreateController();
        var request = new CreateServiceRequest
        {
            ServiceName = "test-service",
            Image = "nginx:latest",
            Labels = new Dictionary<string, string> { ["app"] = "test" },
            Env = new Dictionary<string, string> { ["ENV_VAR"] = "val" },
            Mounts = new List<MountConfig>
            {
                new() { Type = "volume", Source = "vol1", Target = "/data", DriverName = "local" }
            },
            TTY = true,
            DnsNameservers = new List<string> { "8.8.8.8" },
            User = "1000:1000",
            Resources = new ResourcesConfig { MemoryBytes = 1024, NanoCPUs = 1000000 },
            RestartPolicy = new RestartPolicyConfig { Condition = "any", MaxAttempts = 3, Delay = 1000 },
            Placement = new PlacementConfig { Constraints = new List<string> { "node.role == worker" } },
            Networks = new List<string> { "net1" },
            Ports = new List<PortMapping>
            {
                new() { TargetPort = 80, PublishedPort = 8080, Protocol = "tcp" }
            }
        };

        _mockSwarmOperations
            .Setup(x => x.CreateServiceAsync(It.IsAny<ServiceCreateParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceCreateResponse { ID = "new-service-id" });

        var actionResult = await controller.CreateService(request);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<ServiceOperationResponse>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("new-service-id", response.ServiceId);
    }

    [Fact]
    public async Task CreateService_WhenExceptionThrown_Returns500()
    {
        var controller = CreateController();
        _mockSwarmOperations
            .Setup(x => x.CreateServiceAsync(It.IsAny<ServiceCreateParameters>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Docker daemon error"));

        var actionResult = await controller.CreateService(new CreateServiceRequest { ServiceName = "error-svc", Image = "nginx:latest" });
        var statusResult = Assert.IsType<ObjectResult>(actionResult.Result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    [Fact]
    public async Task UpdateService_WhenSuccessful_ReturnsOk()
    {
        var controller = CreateController();
        var existingService = new SwarmService
        {
            ID = "svc-1",
            Version = new global::Docker.DotNet.Models.Version { Index = 10 },
            Spec = new ServiceSpec
            {
                Name = "svc-1",
                TaskTemplate = new TaskSpec
                {
                    ContainerSpec = new ContainerSpec { Image = "old:1" }
                }
            }
        };

        _mockSwarmOperations
            .Setup(x => x.InspectServiceAsync("svc-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingService);

        _mockSwarmOperations
            .Setup(x => x.UpdateServiceAsync("svc-1", It.IsAny<ServiceUpdateParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceUpdateResponse());

        var request = new UpdateServiceRequest
        {
            ServiceId = "svc-1",
            Image = "new:2",
            Labels = new Dictionary<string, string> { ["updated"] = "true" },
            Env = new Dictionary<string, string> { ["A"] = "B" },
            Mounts = new List<MountConfig> { new() { Type = "bind", Source = "/host", Target = "/cont" } },
            Resources = new ResourcesConfig { MemoryBytes = 2048 },
            ForceUpdate = true,
            Networks = new List<string> { "net2" },
            Ports = new List<PortMapping> { new() { TargetPort = 443 } },
            TTY = true,
            DnsNameservers = new List<string> { "1.1.1.1" },
            User = "1000",
            Replicas = 2
        };

        var actionResult = await controller.UpdateService("svc-1", request);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<ServiceOperationResponse>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("svc-1", response.ServiceId);
    }

    [Fact]
    public async Task DeleteService_WhenSuccessful_ReturnsOk()
    {
        var controller = CreateController();
        _mockSwarmOperations
            .Setup(x => x.RemoveServiceAsync("svc-1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var actionResult = await controller.DeleteService("svc-1");
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<ServiceOperationResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task InspectService_WhenSuccessful_ReturnsService()
    {
        var controller = CreateController();
        var service = new SwarmService
        {
            ID = "service-123",
            Spec = new ServiceSpec
            {
                Name = "test-service",
                Labels = new Dictionary<string, string> { ["key1"] = "value1" }
            }
        };

        _mockSwarmOperations
            .Setup(x => x.InspectServiceAsync("service-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(service);

        var actionResult = await controller.InspectService("service-123");
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<ServiceOperationResponse>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("service-123", response.ServiceId);
    }

    [Fact]
    public async Task GetServiceLogs_WhenCalled_ReturnsLogs()
    {
        var controller = CreateController();
        var memoryStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Line 1\nLine 2\n"));
        _mockSwarmOperations
            .Setup(x => x.GetServiceLogsAsync("svc-1", It.IsAny<ServiceLogsParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(memoryStream);

        var actionResult = await controller.GetServiceLogs("svc-1", 100);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<ServiceOperationResponse>(okResult.Value);
        Assert.True(response.Success);
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
