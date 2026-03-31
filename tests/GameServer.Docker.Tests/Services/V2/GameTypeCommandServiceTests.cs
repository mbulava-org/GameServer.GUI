using GameServer.Docker.Dtos.V2;
using GameServer.Docker.Models.V2;
using GameServer.Docker.Repositories.V2;
using GameServer.Docker.Services.V2;
using Moq;

namespace GameServer.Docker.Tests.Services.V2;

public class GameTypeCommandServiceTests
{
    [Fact]
    public async Task CreateAsync_WhenRequestIsValid_ShouldCreateGameType()
    {
        // Arrange
        var repository = new Mock<IGameTypeRepository>();
        repository
            .Setup(x => x.CreateAsync(It.IsAny<GameType>()))
            .ReturnsAsync((GameType value) => value with { Id = 42 });

        var service = new GameTypeCommandService(repository.Object);

        // Act
        var result = await service.CreateAsync(new SaveGameTypeRequestDto
        {
            Key = "minecraft",
            DisplayName = "Minecraft",
            ImageReference = "itzg/minecraft-server"
        });

        // Assert
        Assert.Equal(42, result.Id);
        Assert.Equal("minecraft", result.Key);
        repository.Verify(x => x.CreateAsync(It.Is<GameType>(gt => gt.Key == "minecraft" && gt.ImageReference == "itzg/minecraft-server")), Times.Once);
    }

    [Fact]
    public async Task PublishRevisionAsync_WhenSetAsCurrentRevisionIsTrue_ShouldUpdateRevisionAndSetCurrent()
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
                    new GameTypeRevision { Id = 5, VersionTag = "latest", IsPublished = false }
                ]
            });
        repository
            .Setup(x => x.UpdateRevisionAsync("minecraft", It.IsAny<GameTypeRevision>()))
            .ReturnsAsync((string _, GameTypeRevision revision) => revision);

        var service = new GameTypeCommandService(repository.Object);

        // Act
        var result = await service.PublishRevisionAsync("minecraft", 5, true);

        // Assert
        Assert.True(result.IsPublished);
        repository.Verify(x => x.UpdateRevisionAsync("minecraft", It.Is<GameTypeRevision>(r => r.Id == 5 && r.IsPublished)), Times.Once);
        repository.Verify(x => x.SetCurrentRevisionAsync("minecraft", 5), Times.Once);
    }
}
