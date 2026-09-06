using Docker.DotNet.Models;
using GameServer.API.Interfaces;
using GameServer.API.Models;
using GameServer.API.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;

namespace GameServer.API.Tests.Services;

/// <summary>
/// Tests for ServiceOperationsViaAgent to prevent serialization bugs.
/// Critical: These tests ensure SwarmService and TaskResponse objects
/// are correctly transmitted through JSON without losing type information.
/// </summary>
public class ServiceOperationsViaAgentTests
{
    private readonly Mock<IAgentRegistry> _mockAgentRegistry;
    private readonly Mock<IUdpAgentRegistry> _mockUdpAgentRegistry;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<ServiceOperationsViaAgent>> _mockLogger;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;

    public ServiceOperationsViaAgentTests()
    {
        _mockAgentRegistry = new Mock<IAgentRegistry>();
        _mockUdpAgentRegistry = new Mock<IUdpAgentRegistry>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<ServiceOperationsViaAgent>>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();

        _mockUdpAgentRegistry
            .Setup(x => x.GetAllAgents())
            .Returns(Array.Empty<NodeAgentEndpoint>());

        // Setup HttpClient factory
        var httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);
    }

    private ServiceOperationsViaAgent CreateService()
    {
        return new ServiceOperationsViaAgent(
            _mockAgentRegistry.Object,
            _mockUdpAgentRegistry.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object
        );
    }

    private void SetupManagerAgent()
    {
        var managerAgent = new NodeAgentEndpoint
        {
            NodeId = "manager-node-id",
            NodeName = "manager-node",
            InternalUrl = "http://manager-agent:8080",
            IsHealthy = true,
            IsManagerNode = true
        };

        _mockAgentRegistry.Setup(x => x.GetHealthyManagerAgent())
            .Returns(managerAgent);
    }

    [Fact]
    public async Task ListServicesAsync_ShouldDeserializeFullSwarmServiceObjects()
    {
        // Arrange
        var service = CreateService();
        SetupManagerAgent();

        // Create a realistic SwarmService with nested structure
        var testService = new SwarmService
        {
            ID = "test-service-id",
            Version = new global::Docker.DotNet.Models.Version { Index = 123 },
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow,
            Spec = new ServiceSpec
            {
                Name = "minecraft-server",
                Labels = new Dictionary<string, string>
                {
                    ["gameserver.docker.managed"] = "true",
                    ["gameserver.docker.Id"] = "server-001",
                    ["gameserver.docker.name"] = "My Minecraft Server",
                    ["gameserver.docker.gametype"] = "minecraft"
                },
                TaskTemplate = new TaskSpec
                {
                    ContainerSpec = new ContainerSpec
                    {
                        Image = "minecraft:latest"
                    }
                }
            },
            Endpoint = new Endpoint
            {
                Spec = new EndpointSpec(),
                Ports = new List<PortConfig>
                {
                    new() { PublishedPort = 25565, TargetPort = 25565, Protocol = "tcp" }
                }
            }
        };

        // Agent response with full SwarmService in data
        var agentResponse = new
        {
            success = true,
            message = "Found 1 services",
            data = new Dictionary<string, object>
            {
                ["services"] = new List<SwarmService> { testService }
            }
        };

        var responseJson = JsonSerializer.Serialize(agentResponse);
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(responseMessage);

        // Act
        var result = await service.ListServicesAsync(cancellationToken: CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        
        var resultService = result[0];
        Assert.Equal("test-service-id", resultService.ID);
        
        // CRITICAL: Spec must not be null!
        Assert.NotNull(resultService.Spec);
        Assert.Equal("minecraft-server", resultService.Spec.Name);
        
        // CRITICAL: Labels must be preserved!
        Assert.NotNull(resultService.Spec.Labels);
        Assert.True(resultService.Spec.Labels.ContainsKey("gameserver.docker.managed"));
        Assert.Equal("true", resultService.Spec.Labels["gameserver.docker.managed"]);
        
        // CRITICAL: Nested objects must be preserved!
        Assert.NotNull(resultService.Spec.TaskTemplate);
        Assert.NotNull(resultService.Spec.TaskTemplate.ContainerSpec);
        Assert.Equal("minecraft:latest", resultService.Spec.TaskTemplate.ContainerSpec.Image);
        
        // Ports must be preserved
        Assert.NotNull(resultService.Endpoint);
        Assert.NotNull(resultService.Endpoint.Ports);
        Assert.Single(resultService.Endpoint.Ports);
        Assert.Equal(25565u, resultService.Endpoint.Ports[0].PublishedPort);
    }

    [Fact]
    public async Task InspectServiceAsync_ShouldDeserializeFullSwarmServiceObject()
    {
        // Arrange
        var service = CreateService();
        SetupManagerAgent();

        var testService = new SwarmService
        {
            ID = "test-service-id",
            Spec = new ServiceSpec
            {
                Name = "valheim-server",
                Labels = new Dictionary<string, string>
                {
                    ["gameserver.docker.managed"] = "true",
                    ["gameserver.docker.gametype"] = "valheim"
                }
            }
        };

        var agentResponse = new
        {
            success = true,
            serviceId = "test-service-id",
            message = "Service retrieved successfully",
            data = new Dictionary<string, object>
            {
                ["service"] = testService
            }
        };

        var responseJson = JsonSerializer.Serialize(agentResponse);
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery.Contains("/api/services/test-service-id")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(responseMessage);

        // Act
        var result = await service.InspectServiceAsync("test-service-id");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-service-id", result.ID);
        
        // CRITICAL: Spec must not be null!
        Assert.NotNull(result.Spec);
        Assert.Equal("valheim-server", result.Spec.Name);
        
        // CRITICAL: Labels must be preserved!
        Assert.NotNull(result.Spec.Labels);
        Assert.Equal("true", result.Spec.Labels["gameserver.docker.managed"]);
        Assert.Equal("valheim", result.Spec.Labels["gameserver.docker.gametype"]);
    }

    [Fact]
    public async Task ListTasksAsync_ShouldDeserializeFullTaskResponseObjects()
    {
        // Arrange
        var service = CreateService();
        SetupManagerAgent();

        var testTask = new TaskResponse
        {
            ID = "test-task-id",
            ServiceID = "test-service-id",
            NodeID = "test-node-id",
            Status = new global::Docker.DotNet.Models.TaskStatus
            {
                State = TaskState.Running,
                ContainerStatus = new ContainerStatus
                {
                    ContainerID = "container-123"
                }
            },
            DesiredState = TaskState.Running
        };

        var agentResponse = new
        {
            success = true,
            count = 1,
            tasks = new List<TaskResponse> { testTask }
        };

        var responseJson = JsonSerializer.Serialize(agentResponse);
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery.Contains("/api/tasks")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(responseMessage);

        // Act
        var result = await service.ListTasksAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        
        var resultTask = result[0];
        Assert.Equal("test-task-id", resultTask.ID);
        Assert.Equal("test-service-id", resultTask.ServiceID);
        
        // CRITICAL: Nested status must be preserved!
        Assert.NotNull(resultTask.Status);
        Assert.Equal(TaskState.Running, resultTask.Status.State);
        Assert.NotNull(resultTask.Status.ContainerStatus);
        Assert.Equal("container-123", resultTask.Status.ContainerStatus.ContainerID);
    }

    [Fact]
    public async Task ListServicesAsync_WithMissingSpec_ShouldHandleGracefully()
    {
        // Arrange
        var service = CreateService();
        SetupManagerAgent();

        // Simulate corrupted service without Spec (shouldn't happen, but defensive)
        var testService = new SwarmService
        {
            ID = "test-service-id",
            Spec = null  // Missing Spec!
        };

        var agentResponse = new
        {
            success = true,
            data = new Dictionary<string, object>
            {
                ["services"] = new List<SwarmService> { testService }
            }
        };

        var responseJson = JsonSerializer.Serialize(agentResponse);
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(responseMessage);

        // Act
        var result = await service.ListServicesAsync(cancellationToken: CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Null(result[0].Spec);  // Should handle null Spec without crashing
    }

    [Fact]
    public async Task CreateServiceAsync_WhenAgentReturnsSuccess_ReturnsServiceCreateResponse()
    {
        var service = CreateService();
        SetupManagerAgent();

        var agentResponse = new
        {
            success = true,
            serviceId = "new-service-123",
            message = "Created"
        };
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(agentResponse), System.Text.Encoding.UTF8, "application/json")
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post && req.RequestUri!.PathAndQuery.Contains("/api/services")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(responseMessage);

        var parameters = new ServiceCreateParameters
        {
            Service = new ServiceSpec
            {
                Name = "test-create-svc",
                TaskTemplate = new TaskSpec
                {
                    ContainerSpec = new ContainerSpec
                    {
                        Image = "alpine:latest",
                        Env = new List<string> { "FOO=BAR", "BAZ=QUX" },
                        Mounts = new List<Mount>
                        {
                            new()
                            {
                                Type = "volume",
                                Source = "testvol",
                                Target = "/data",
                                VolumeOptions = new VolumeOptions
                                {
                                    DriverConfig = new Driver
                                    {
                                        Name = "local",
                                        Options = new Dictionary<string, string> { ["opt1"] = "val1" }
                                    }
                                }
                            }
                        }
                    },
                    Resources = new ResourceRequirements
                    {
                        Limits = new SwarmLimit { MemoryBytes = 1024 * 1024 * 512, NanoCPUs = 1000000000 }
                    },
                    RestartPolicy = new SwarmRestartPolicy { Condition = "on-failure", Delay = TimeSpan.FromSeconds(5), MaxAttempts = 3 },
                    Placement = new Placement { Constraints = new List<string> { "node.role == worker" } },
                    Networks = new List<NetworkAttachmentConfig> { new() { Target = "gameserver-net" } }
                },
                EndpointSpec = new EndpointSpec
                {
                    Ports = new List<PortConfig>
                    {
                        new() { TargetPort = 8080, PublishedPort = 80, Protocol = "tcp", PublishMode = "ingress" }
                    }
                }
            }
        };

        var result = await service.CreateServiceAsync(parameters);
        Assert.NotNull(result);
        Assert.Equal("new-service-123", result.ID);
    }

    [Fact]
    public async Task CreateServiceAsync_WhenAgentReturnsFailure_ThrowsInvalidOperationException()
    {
        var service = CreateService();
        SetupManagerAgent();

        var agentResponse = new
        {
            success = false,
            message = "Port conflict"
        };
        var responseMessage = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(JsonSerializer.Serialize(agentResponse), System.Text.Encoding.UTF8, "application/json")
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(responseMessage);

        var parameters = new ServiceCreateParameters
        {
            Service = new ServiceSpec
            {
                Name = "fail-svc",
                TaskTemplate = new TaskSpec
                {
                    ContainerSpec = new ContainerSpec { Image = "img" }
                }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateServiceAsync(parameters));
        Assert.Contains("Port conflict", ex.Message);
    }

    [Fact]
    public async Task UpdateServiceAsync_WhenAgentReturnsSuccess_CompletesSuccessfully()
    {
        var service = CreateService();
        SetupManagerAgent();

        var agentResponse = new { success = true, serviceId = "svc-upd", message = "Updated" };
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(agentResponse), System.Text.Encoding.UTF8, "application/json")
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Put && req.RequestUri!.PathAndQuery.Contains("/api/services/svc-upd")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(responseMessage);

        var parameters = new ServiceUpdateParameters
        {
            Service = new ServiceSpec
            {
                TaskTemplate = new TaskSpec
                {
                    ContainerSpec = new ContainerSpec { Image = "alpine:3.19", Env = new List<string> { "A=B" } },
                    ForceUpdate = 1
                },
                Mode = new ServiceMode { Replicated = new ReplicatedService { Replicas = 1 } }
            }
        };

        await service.UpdateServiceAsync("svc-upd", parameters);
    }

    [Fact]
    public async Task RemoveServiceAsync_WhenAgentReturnsSuccess_CompletesSuccessfully()
    {
        var service = CreateService();
        SetupManagerAgent();

        var agentResponse = new { success = true, serviceId = "svc-del", message = "Deleted" };
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(agentResponse), System.Text.Encoding.UTF8, "application/json")
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Delete && req.RequestUri!.PathAndQuery.Contains("/api/services/svc-del")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(responseMessage);

        await service.RemoveServiceAsync("svc-del");
    }

    [Fact]
    public async Task ListNetworksAsync_WithFilter_ReturnsNetworks()
    {
        var service = CreateService();
        SetupManagerAgent();

        var testNetwork = new NetworkResponse { ID = "net-1", Name = "gameserver-bridge", Driver = "overlay" };
        var agentResponse = new
        {
            success = true,
            count = 1,
            networks = new List<NetworkResponse> { testNetwork }
        };
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(agentResponse), System.Text.Encoding.UTF8, "application/json")
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery.Contains("/api/networks")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(responseMessage);

        var parameters = new NetworksListParameters
        {
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["name"] = new Dictionary<string, bool> { ["gameserver-bridge"] = true }
            }
        };

        var result = await service.ListNetworksAsync(parameters);
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("gameserver-bridge", result[0].Name);
    }

    [Fact]
    public async Task InspectNetworkAsync_ReturnsNetwork()
    {
        var service = CreateService();
        SetupManagerAgent();

        var testNetwork = new NetworkResponse { ID = "net-123", Name = "overlay-net", Driver = "overlay" };
        var agentResponse = new
        {
            success = true,
            network = testNetwork
        };
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(agentResponse), System.Text.Encoding.UTF8, "application/json")
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery.Contains("/api/networks/net-123")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(responseMessage);

        var result = await service.InspectNetworkAsync("net-123");
        Assert.NotNull(result);
        Assert.Equal("net-123", result.ID);
        Assert.Equal("overlay-net", result.Name);
    }

    [Fact]
    public async Task GetManagerAgent_WhenNotInRegistry_FallsBackToUdpRegistry()
    {
        var service = CreateService();
        _mockAgentRegistry.Setup(x => x.GetHealthyManagerAgent()).Returns((NodeAgentEndpoint?)null);

        var udpAgent = new NodeAgentEndpoint
        {
            NodeId = "udp-node-1",
            NodeName = "udp-node",
            InternalUrl = "http://udp-agent:8080",
            IsHealthy = true,
            IsManagerNode = true
        };
        _mockUdpAgentRegistry.Setup(x => x.GetAllAgents()).Returns(new List<NodeAgentEndpoint> { udpAgent });

        var agentResponse = new { success = true, serviceId = "svc-udp", message = "Deleted" };
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(agentResponse), System.Text.Encoding.UTF8, "application/json")
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(responseMessage);

        await service.RemoveServiceAsync("svc-udp");
    }
}
