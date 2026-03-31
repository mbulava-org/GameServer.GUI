using Docker.DotNet;
using GameServer.Docker.Dtos.V2;
using GameServer.Docker.Models.V2;
using GameServer.Docker.Repositories.V2;
using GameServer.Docker.Services.V2.Detection;
using Microsoft.Extensions.Logging;
using Moq;

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
                Key = "minecraft",
                ImageReference = "itzg/minecraft-server"
            });

        var service = new GameTypeSetupDetectionService(repository.Object, dockerClient: null, Mock.Of<ILogger<GameTypeSetupDetectionService>>());

        // Act
        var action = () => service.DetectAsync("minecraft", new DetectGameTypeSetupRequestDto { VersionTag = "latest" });

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

        var dockerClient = new Mock<IDockerClient>();
        var service = new GameTypeSetupDetectionService(repository.Object, dockerClient.Object, Mock.Of<ILogger<GameTypeSetupDetectionService>>());

        // Act
        var action = () => service.DetectAsync("missing", new DetectGameTypeSetupRequestDto { VersionTag = "latest" });

        // Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(action);
    }
}
