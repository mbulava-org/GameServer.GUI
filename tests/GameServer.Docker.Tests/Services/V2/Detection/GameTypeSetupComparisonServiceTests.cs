using Docker.DotNet;
using Docker.DotNet.Models;
using GameServer.Docker.Dtos.V2;
using GameServer.Docker.Models.V2;
using GameServer.Docker.Repositories.V2;
using GameServer.Docker.Services.V2.Detection;
using Microsoft.Extensions.Logging;
using Moq;

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
                ImageReference = "itzg/minecraft-server",
                Revisions =
                [
                    new GameTypeRevision
                    {
                        Id = 5,
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

        var imageOperations = new Mock<IImageOperations>();
        imageOperations
            .Setup(x => x.InspectImageAsync("itzg/minecraft-server:latest", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageInspectResponse
            {
                RepoDigests = ["itzg/minecraft-server@sha256:new"],
                Config = new Config
                {
                    Env = ["EULA=TRUE", "DIFFICULTY=hard"],
                    ExposedPorts = new Dictionary<string, EmptyStruct>
                    {
                        ["25565/tcp"] = default,
                        ["25566/tcp"] = default
                    },
                    Volumes = new Dictionary<string, EmptyStruct>
                    {
                        ["/config"] = default
                    }
                }
            });

        var dockerClient = new Mock<IDockerClient>();
        dockerClient.SetupGet(x => x.Images).Returns(imageOperations.Object);

        var service = new GameTypeSetupDetectionService(repository.Object, dockerClient.Object, Mock.Of<ILogger<GameTypeSetupDetectionService>>());

        // Act
        var result = await service.CompareAsync("minecraft", new CompareGameTypeSetupRequestDto
        {
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
                Key = "minecraft",
                ImageReference = "itzg/minecraft-server"
            });

        var service = new GameTypeSetupDetectionService(repository.Object, Mock.Of<IDockerClient>(), Mock.Of<ILogger<GameTypeSetupDetectionService>>());

        // Act
        var action = () => service.CompareAsync("minecraft", new CompareGameTypeSetupRequestDto
        {
            VersionTag = "latest",
            RevisionId = 99
        });

        // Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(action);
    }
}
