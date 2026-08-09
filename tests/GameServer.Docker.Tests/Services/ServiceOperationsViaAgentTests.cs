using Docker.DotNet.Models;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Models;
using GameServer.Docker.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;

namespace GameServer.Docker.Tests.Services;

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
    public async Task ListServicesAsync_WhenNoManagerAvailable_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var service = CreateService();
        
        _mockAgentRegistry.Setup(x => x.GetHealthyManagerAgent())
            .Returns((NodeAgentEndpoint?)null);
        
        _mockAgentRegistry.Setup(x => x.GetAllAgents())
            .Returns(new List<NodeAgentEndpoint>());
        
        _mockAgentRegistry.Setup(x => x.GetManagerAgents())
            .Returns(new List<NodeAgentEndpoint>());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ListServicesAsync(cancellationToken: CancellationToken.None));
        
        Assert.Contains("No healthy manager agent available", exception.Message);
    }
}
