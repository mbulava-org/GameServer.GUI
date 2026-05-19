using GameServerModel = GameServer.Docker.Models.V2.GameServer;
using GameServerSettingModel = GameServer.Docker.Models.V2.GameServerSetting;
using GameTypeModel = GameServer.Docker.Models.V2.GameType;
using GameTypePortModel = GameServer.Docker.Models.V2.GameTypePort;
using GameTypeRevisionModel = GameServer.Docker.Models.V2.GameTypeRevision;
using GameTypeVolumeModel = GameServer.Docker.Models.V2.GameTypeVolume;
using GameTypeWebHostModel = GameServer.Docker.Models.V2.GameTypeWebHost;
using GameServer.Docker.Repositories.V2;
using GameServer.Docker.Services.V2;
using Moq;

namespace GameServer.Docker.Tests.Services.V2;

public class GameServerQueryServiceTests
{
    [Fact]
    public async Task GetListAsync_WhenServerRevisionExists_ShouldProjectListItem()
    {
        // Arrange
        var serverRepository = new Mock<IGameServerRepository>();
        serverRepository
            .Setup(x => x.GetAllAsync(false))
            .ReturnsAsync(
            [
                new GameServerModel
                {
                    Id = 1,
                    ServerId = "srv-1",
                    Name = "Minecraft Survival",
                    Description = "Primary world",
                    GameTypeRevisionId = 10,
                    ServiceName = "minecraft-survival",
                    Status = "Running",
                    UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            ]);

        var gameTypeRepository = new Mock<IGameTypeRepository>();
        gameTypeRepository
            .Setup(x => x.GetAllAsync(true))
            .ReturnsAsync(
            [
                new GameTypeModel
                {
                    Id = 2,
                    Key = "minecraft",
                    DisplayName = "Minecraft",
                    ThumbnailUrl = "https://example.test/thumb.png",
                    Revisions =
                    [
                        new GameTypeRevisionModel
                        {
                            Id = 10,
                            VersionTag = "1.21.2",
                            ImageReference = "itzg/minecraft-server:java21",
                            Ports =
                            [
                                new GameTypePortModel
                                {
                                    ContainerPort = 25565,
                                    Protocol = "tcp",
                                    AdvertisedPort = true,
                                    Description = "Game Port",
                                    DisplayOrder = 0
                                }
                            ]
                        }
                    ]
                }
            ]);

        var service = new GameServerQueryService(serverRepository.Object, gameTypeRepository.Object);

        // Act
        var result = await service.GetListAsync(includeDeleted: false);

        // Assert
        var item = Assert.Single(result);
        Assert.Equal("srv-1", item.ServerId);
        Assert.Equal("Minecraft", item.GameTypeDisplayName);
        Assert.Equal("1.21.2", item.RevisionVersionTag);
        Assert.Equal("itzg/minecraft-server:java21", item.RevisionImageReference);
        Assert.Equal(25565, Assert.Single(item.ResolvedPorts).ContainerPort);
    }

    [Fact]
    public async Task GetByServerIdAsync_WhenServerExists_ShouldProjectDetail()
    {
        // Arrange
        var serverRepository = new Mock<IGameServerRepository>();
        serverRepository
            .Setup(x => x.GetByServerIdAsync("srv-1"))
            .ReturnsAsync(new GameServerModel
            {
                Id = 1,
                ServerId = "srv-1",
                Name = "Minecraft Survival",
                GameTypeRevisionId = 10,
                ServiceName = "minecraft-survival",
                Status = "Running",
                Settings =
                [
                    new GameServerSettingModel { Id = 1, SettingKey = "EULA", Value = "TRUE" }
                ]
            });

        var gameTypeRepository = new Mock<IGameTypeRepository>();
        gameTypeRepository
            .Setup(x => x.GetAllAsync(true))
            .ReturnsAsync(
            [
                new GameTypeModel
                {
                    Id = 2,
                    Key = "minecraft",
                    DisplayName = "Minecraft",
                    Description = "Sandbox server",
                    ThumbnailUrl = "https://example.test/thumb.png",
                    Revisions =
                    [
                        new GameTypeRevisionModel
                        {
                            Id = 10,
                            VersionTag = "1.21.2",
                            ImageReference = "itzg/minecraft-server:java21",
                            Ports =
                            [
                                new GameTypePortModel { ContainerPort = 25565, Protocol = "tcp", AdvertisedPort = true, DisplayOrder = 0 }
                            ],
                            Volumes =
                            [
                                new GameTypeVolumeModel { Source = "/data", Usage = "world", DisplayOrder = 0 }
                            ],
                            WebHosts =
                            [
                                new GameTypeWebHostModel { Name = "Dynmap", ContainerPort = 8123, DisplayOrder = 0 }
                            ]
                        }
                    ]
                }
            ]);

        var service = new GameServerQueryService(serverRepository.Object, gameTypeRepository.Object);

        // Act
        var result = await service.GetByServerIdAsync("srv-1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Minecraft", result.GameTypeDisplayName);
        Assert.Equal("itzg/minecraft-server:java21", result.RevisionImageReference);
        Assert.Equal("EULA", Assert.Single(result.Settings).SettingKey);
        Assert.Equal("world", Assert.Single(result.ResolvedVolumes).Usage);
        Assert.Equal("Dynmap", Assert.Single(result.ResolvedWebHosts).Name);
        Assert.Empty(result.DockerVolumeOptions);
        Assert.Empty(result.NetworkOptions);
        Assert.Empty(result.ConfigurationRules);
    }
}
