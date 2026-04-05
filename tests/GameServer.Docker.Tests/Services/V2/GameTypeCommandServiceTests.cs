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
            Type = "docker"
        });

        // Assert
        Assert.Equal(42, result.Id);
        Assert.Equal("minecraft", result.Key);
        repository.Verify(x => x.CreateAsync(It.Is<GameType>(gt => gt.Key == "minecraft" && gt.Type == "docker")), Times.Once);
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

    [Fact]
    public async Task AddRevisionAsync_WhenPortSettingHasNoMappings_ShouldThrowArgumentException()
    {
        // Arrange
        var repository = new Mock<IGameTypeRepository>();
        var service = new GameTypeCommandService(repository.Object);

        // Act
        var action = () => service.AddRevisionAsync("minecraft", new SaveGameTypeRevisionRequestDto
        {
            VersionTag = "latest",
            ImageReference = "itzg/minecraft-server",
            Ports =
            [
                new GameTypePortDto { ContainerPort = 25565, Protocol = "tcp", AdvertisedPort = true }
            ],
            SettingDefinitions =
            [
                new GameTypeSettingDefinitionDto
                {
                    SettingKey = "SERVER_PORT",
                    DefaultValue = "25565",
                    Metadata = new GameTypeSettingMetadataDto
                    {
                        DataType = "port",
                        PortMappings = []
                    }
                }
            ]
        });

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(action);
        repository.Verify(x => x.AddRevisionAsync(It.IsAny<string>(), It.IsAny<GameTypeRevision>()), Times.Never);
    }

    [Fact]
    public async Task AddRevisionAsync_WhenRelatedMappingIsNotCalculatedFromPrimary_ShouldThrowArgumentException()
    {
        // Arrange
        var repository = new Mock<IGameTypeRepository>();
        var service = new GameTypeCommandService(repository.Object);

        // Act
        var action = () => service.AddRevisionAsync("minecraft", new SaveGameTypeRevisionRequestDto
        {
            VersionTag = "latest",
            ImageReference = "itzg/minecraft-server",
            Ports =
            [
                new GameTypePortDto { ContainerPort = 25565, Protocol = "tcp", AdvertisedPort = true },
                new GameTypePortDto { ContainerPort = 25567, Protocol = "udp", AdvertisedPort = false }
            ],
            SettingDefinitions =
            [
                new GameTypeSettingDefinitionDto
                {
                    SettingKey = "SERVER_PORT",
                    DefaultValue = "25565",
                    Metadata = new GameTypeSettingMetadataDto
                    {
                        DataType = "port",
                        PortMappings =
                        [
                            new GameTypeSettingPortMappingDto
                            {
                                MappingRole = GameTypeSettingPortMappingRole.Primary.ToString(),
                                RelationType = GameTypeSettingPortRelationType.Direct.ToString(),
                                TargetContainerPort = 25565,
                                TargetProtocol = "tcp"
                            },
                            new GameTypeSettingPortMappingDto
                            {
                                MappingRole = GameTypeSettingPortMappingRole.Related.ToString(),
                                RelationType = GameTypeSettingPortRelationType.Offset.ToString(),
                                TargetContainerPort = 25567,
                                TargetProtocol = "udp",
                                CalculationValue = 1
                            }
                        ]
                    }
                }
            ]
        });

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(action);
        repository.Verify(x => x.AddRevisionAsync(It.IsAny<string>(), It.IsAny<GameTypeRevision>()), Times.Never);
    }
}
