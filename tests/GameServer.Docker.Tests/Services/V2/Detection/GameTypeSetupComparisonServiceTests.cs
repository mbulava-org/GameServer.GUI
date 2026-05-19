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

public class GameTypeSetupComparisonServiceTests
{
    [Fact]
    public async Task CompareAsync_WhenDetectedSetupDiffers_ShouldReportStructuredDifferences()
    {
        // Arrange
        var repository = new Mock<IGameTypeRepository>();
        repository
            .Setup(x => x.GetByKeyAsync("minecraft"))
            .ReturnsAsync(new GameType
            {
                Key = "minecraft",
                Revisions =
                [
                    new GameTypeRevision
                    {
                        Id = 5,
                        ImageReference = "itzg/minecraft-server",
                        VersionTag = "1.21.1",
                        ImageDigest = "sha256:old",
                        Ports =
                        [
                            new GameTypePort { ContainerPort = 25565, Protocol = "tcp", AdvertisedPort = true }
                        ],
                        Volumes =
                        [
                            new GameTypeVolume { Source = "data", Description = "/data", Usage = "world" }
                        ],
                        SettingDefinitions =
                        [
                            new GameTypeSettingDefinition { SettingKey = "EULA", DefaultValue = "FALSE" },
                            new GameTypeSettingDefinition { SettingKey = "MODE", DefaultValue = "survival" }
                        ]
                    }
                ]
            });

        var agentRegistry = new Mock<IAgentRegistry>();
        agentRegistry
            .Setup(x => x.GetHealthyAgents())
            .Returns(
            [
                new NodeAgentEndpoint
                {
                    NodeId = "agent-1",
                    NodeName = "agent-1",
                    InternalUrl = "http://agent-1:8080",
                    IsHealthy = true,
                    IsManagerNode = true
                }
            ]);

        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    RepoDigests = new[] { "itzg/minecraft-server@sha256:new" },
                    EnvironmentVariables = new[] { "EULA=TRUE", "DIFFICULTY=hard" },
                    ExposedPorts = new[] { "25565/tcp", "25566/tcp" },
                    VolumePaths = new[] { "/config" }
                })
            });

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(httpMessageHandler.Object));

        var service = new GameTypeSetupDetectionService(
            repository.Object,
            agentRegistry.Object,
            httpClientFactory.Object,
            Mock.Of<ILogger<GameTypeSetupDetectionService>>());

        // Act
        var result = await service.CompareAsync("minecraft", new CompareGameTypeSetupRequestDto
        {
            ImageReference = "itzg/minecraft-server",
            VersionTag = "latest",
            RevisionId = 5
        });

        // Assert
        Assert.True(result.HasChanges);
        Assert.True(result.DigestChanged);
        Assert.Contains("25566/tcp", result.AddedPorts);
        Assert.Contains("/config", result.AddedVolumes);
        Assert.Contains("/data", result.RemovedVolumes);
        Assert.Contains("DIFFICULTY", result.AddedSettings);
        Assert.Contains("MODE", result.RemovedSettings);
        var changedSetting = Assert.Single(result.ChangedSettings);
        Assert.Equal("EULA", changedSetting.Key);
        Assert.Equal("FALSE", changedSetting.RevisionValue);
        Assert.Equal("TRUE", changedSetting.DetectedValue);
    }

    [Fact]
    public async Task CompareAsync_WhenRevisionMissing_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var repository = new Mock<IGameTypeRepository>();
        repository
            .Setup(x => x.GetByKeyAsync("minecraft"))
            .ReturnsAsync(new GameType
            {
                Key = "minecraft"
            });

        var service = new GameTypeSetupDetectionService(
            repository.Object,
            Mock.Of<IAgentRegistry>(),
            Mock.Of<IHttpClientFactory>(),
            Mock.Of<ILogger<GameTypeSetupDetectionService>>());

        // Act
        var action = () => service.CompareAsync("minecraft", new CompareGameTypeSetupRequestDto
        {
            ImageReference = "itzg/minecraft-server",
            VersionTag = "latest",
            RevisionId = 99
        });

        // Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(action);
    }
}
