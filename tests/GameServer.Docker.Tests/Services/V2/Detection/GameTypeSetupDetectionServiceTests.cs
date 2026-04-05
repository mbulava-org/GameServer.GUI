using GameServer.Docker.Dtos.V2;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Models;
using GameServer.Docker.Models.V2;
using GameServer.Docker.Repositories.V2;
using GameServer.Docker.Services.V2.Detection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http.Json;

namespace GameServer.Docker.Tests.Services.V2.Detection;

public class GameTypeSetupDetectionServiceTests
{
    [Fact]
    public async Task DetectAsync_WhenDockerClientUnavailable_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var repository = new Mock<IGameTypeRepository>();
        repository
            .Setup(x => x.GetByKeyAsync("minecraft"))
            .ReturnsAsync(new GameType
            {
                Key = "minecraft"
            });

        var agentRegistry = new Mock<IAgentRegistry>();
        agentRegistry.Setup(x => x.GetHealthyAgents()).Returns([]);

        var service = new GameTypeSetupDetectionService(
            repository.Object,
            agentRegistry.Object,
            Mock.Of<IHttpClientFactory>(),
            Mock.Of<ILogger<GameTypeSetupDetectionService>>());

        // Act
        var action = () => service.DetectAsync("minecraft", new DetectGameTypeSetupRequestDto { ImageReference = "itzg/minecraft-server", VersionTag = "latest" });

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(action);
    }

    [Fact]
    public async Task DetectAsync_WhenGameTypeMissing_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var repository = new Mock<IGameTypeRepository>();
        repository
            .Setup(x => x.GetByKeyAsync("missing"))
            .ReturnsAsync((GameType?)null);

        var service = new GameTypeSetupDetectionService(
            repository.Object,
            Mock.Of<IAgentRegistry>(),
            Mock.Of<IHttpClientFactory>(),
            Mock.Of<ILogger<GameTypeSetupDetectionService>>());

        // Act
        var action = () => service.DetectAsync("missing", new DetectGameTypeSetupRequestDto { ImageReference = "itzg/minecraft-server", VersionTag = "latest" });

        // Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(action);
    }

    [Fact]
    public async Task DetectAsync_WhenPortSettingMatchesDefinedPort_ShouldInferDirectMapping()
    {
        // Arrange
        var service = CreateService(
            environmentVariables: ["SERVER_PORT=25565"],
            exposedPorts: ["25565/tcp"]);

        // Act
        var result = await service.DetectAsync("minecraft", new DetectGameTypeSetupRequestDto { ImageReference = "itzg/minecraft-server", VersionTag = "latest" });

        // Assert
        var setting = Assert.Single(result.Settings);
        var mapping = Assert.Single(setting.PortMappings);
        Assert.Equal("25565", setting.DefaultValue);
        Assert.Equal(GameTypeSettingPortMappingRole.Primary.ToString(), mapping.MappingRole);
        Assert.Equal(GameTypeSettingPortRelationType.Direct.ToString(), mapping.RelationType);
        Assert.Equal(25565, mapping.TargetContainerPort);
        Assert.Equal("tcp", mapping.TargetProtocol);
    }

    [Fact]
    public async Task DetectAsync_WhenPrimaryPortSettingHasMultipleExposedPorts_ShouldInferRelatedMappings()
    {
        // Arrange
        var service = CreateService(
            environmentVariables: ["SERVER_PORT=25565"],
            exposedPorts: ["25565/tcp", "25565/udp", "25566/udp"]);

        // Act
        var result = await service.DetectAsync("minecraft", new DetectGameTypeSetupRequestDto { ImageReference = "itzg/minecraft-server", VersionTag = "latest" });

        // Assert
        var setting = Assert.Single(result.Settings);
        Assert.Equal(3, setting.PortMappings.Count);
        Assert.Contains(setting.PortMappings, mapping =>
            mapping.MappingRole == GameTypeSettingPortMappingRole.Primary.ToString()
            && mapping.RelationType == GameTypeSettingPortRelationType.Direct.ToString()
            && mapping.TargetContainerPort == 25565
            && mapping.TargetProtocol == "tcp");
        Assert.Contains(setting.PortMappings, mapping =>
            mapping.MappingRole == GameTypeSettingPortMappingRole.Related.ToString()
            && mapping.RelationType == GameTypeSettingPortRelationType.Offset.ToString()
            && mapping.TargetContainerPort == 25565
            && mapping.TargetProtocol == "udp");
        Assert.Contains(setting.PortMappings, mapping =>
            mapping.MappingRole == GameTypeSettingPortMappingRole.Related.ToString()
            && mapping.RelationType == GameTypeSettingPortRelationType.Offset.ToString()
            && mapping.TargetContainerPort == 25566
            && mapping.TargetProtocol == "udp"
            && mapping.CalculationValue == 1);
    }

    [Fact]
    public async Task DetectAsync_WhenFirstAgentCannotInspectImage_ShouldTryNextHealthyAgent()
    {
        // Arrange
        var pullIfMissingRequested = false;
        var service = CreateService(
            request =>
            {
                using var document = System.Text.Json.JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
                pullIfMissingRequested = document.RootElement.TryGetProperty("pullIfMissing", out var pullIfMissingProperty)
                    && pullIfMissingProperty.ValueKind == System.Text.Json.JsonValueKind.True;

                return request.RequestUri?.Host switch
                {
                    "agent-a" => new HttpResponseMessage(HttpStatusCode.NotFound)
                    {
                        Content = JsonContent.Create(new { error = "Image not found on this node." })
                    },
                    _ => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new
                        {
                            RepoDigests = new[] { "itzg/minecraft-server@sha256:test" },
                            EnvironmentVariables = new[] { "SERVER_PORT=25565" },
                            ExposedPorts = new[] { "25565/tcp" },
                            VolumePaths = Array.Empty<string>()
                        })
                    }
                };
            },
            new NodeAgentEndpoint { NodeId = "agent-a", NodeName = "agent-a", InternalUrl = "http://agent-a:8080", IsHealthy = true },
            new NodeAgentEndpoint { NodeId = "agent-b", NodeName = "agent-b", InternalUrl = "http://agent-b:8080", IsHealthy = true });

        // Act
        var result = await service.DetectAsync("minecraft", new DetectGameTypeSetupRequestDto { ImageReference = "itzg/minecraft-server", VersionTag = "latest" });

        // Assert
        Assert.Equal("sha256:test", result.ImageDigest);
        Assert.Single(result.Settings);
        Assert.True(pullIfMissingRequested);
    }

    private static GameTypeSetupDetectionService CreateService(IList<string> environmentVariables, IReadOnlyList<string> exposedPorts)
    {
        return CreateService(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    RepoDigests = new[] { "itzg/minecraft-server@sha256:test" },
                    EnvironmentVariables = environmentVariables,
                    ExposedPorts = exposedPorts,
                    VolumePaths = Array.Empty<string>()
                })
            },
            new NodeAgentEndpoint
            {
                NodeId = "agent-1",
                NodeName = "agent-1",
                InternalUrl = "http://agent-1:8080",
                IsHealthy = true,
                IsManagerNode = true
            });
    }

    private static GameTypeSetupDetectionService CreateService(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory,
        params NodeAgentEndpoint[] agents)
    {
        var repository = new Mock<IGameTypeRepository>();
        repository
            .Setup(x => x.GetByKeyAsync("minecraft"))
            .ReturnsAsync(new GameType
            {
                Key = "minecraft"
            });

        var agentRegistry = new Mock<IAgentRegistry>();
        agentRegistry.Setup(x => x.GetHealthyAgents()).Returns(agents.ToList());

        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) => responseFactory(request));

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(httpMessageHandler.Object));

        return new GameTypeSetupDetectionService(
            repository.Object,
            agentRegistry.Object,
            httpClientFactory.Object,
            Mock.Of<ILogger<GameTypeSetupDetectionService>>());
    }
}
