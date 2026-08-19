using GameServer.API.Models.V2;
using GameServer.API.Repositories.V2;
using GameServer.API.Services.V2;
using Moq;

namespace GameServer.API.Tests.Services.V2;

public class GameTypeQueryServiceTests
{
    [Fact]
    public async Task GetListAsync_WhenCurrentRevisionExists_ShouldProjectListItem()
    {
        // Arrange
        var repository = new Mock<IGameTypeRepository>();
        repository
            .Setup(x => x.GetAllAsync(false))
            .ReturnsAsync(
            [
                new GameType
                {
                    Id = 1,
                    Key = "minecraft",
                    DisplayName = "Minecraft",
                    Type = "docker",
                    IsActive = true,
                    CurrentRevisionId = 10,
                    UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    Revisions =
                    [
                        new GameTypeRevision { Id = 9, ImageReference = "itzg/minecraft-server", VersionTag = "1.21.1", IsPublished = true, CreatedAt = new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc) },
                        new GameTypeRevision { Id = 10, ImageReference = "itzg/minecraft-server", VersionTag = "1.21.2", IsPublished = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
                    ]
                }
            ]);

        var service = new GameTypeQueryService(repository.Object);

        // Act
        var result = await service.GetListAsync(includeInactive: false);

        // Assert
        var item = Assert.Single(result);
        Assert.Equal("minecraft", item.Key);
        Assert.Equal("docker", item.Type);
        Assert.Equal("itzg/minecraft-server", item.CurrentImageReference);
        Assert.Equal(10, item.CurrentRevisionId);
        Assert.Equal("1.21.2", item.CurrentVersionTag);
        Assert.Equal(2, item.RevisionCount);
        Assert.Equal(2, item.PublishedRevisionCount);
    }

    [Fact]
    public async Task GetByKeyAsync_WhenGameTypeExists_ShouldProjectRevisionDetail()
    {
        // Arrange
        var repository = new Mock<IGameTypeRepository>();
        repository
            .Setup(x => x.GetByKeyAsync("minecraft"))
            .ReturnsAsync(new GameType
            {
                Id = 1,
                Key = "minecraft",
                DisplayName = "Minecraft",
                Type = "docker",
                CurrentRevisionId = 10,
                Revisions =
                [
                    new GameTypeRevision
                    {
                        Id = 10,
                        ImageReference = "itzg/minecraft-server",
                        VersionTag = "1.21.2",
                        IsPublished = true,
                        Ports =
                        [
                            new GameTypePort { Id = 100, ContainerPort = 25565, Protocol = "tcp", AdvertisedPort = true, DisplayOrder = 0 }
                        ],
                        SettingDefinitions =
                        [
                            new GameTypeSettingDefinition
                            {
                                Id = 200,
                                SettingKey = "SERVER_PORT",
                                Metadata = new GameTypeSettingMetadata
                                {
                                    Id = 300,
                                    DataType = "port",
                                    PortMappings =
                                    [
                                        new GameTypeSettingPortMapping
                                        {
                                            Id = 400,
                                            MappingRole = GameTypeSettingPortMappingRole.Primary,
                                            RelationType = GameTypeSettingPortRelationType.Direct,
                                            TargetContainerPort = 25565,
                                            TargetProtocol = "tcp",
                                            DisplayOrder = 0
                                        }
                                    ]
                                }
                            }
                        ]
                    }
                ]
            });

        var service = new GameTypeQueryService(repository.Object);

        // Act
        var result = await service.GetByKeyAsync("minecraft");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("minecraft", result.Key);
        Assert.Equal("docker", result.Type);
        var revision = Assert.Single(result.Revisions);
        Assert.Equal("itzg/minecraft-server", revision.ImageReference);
        Assert.Equal("1.21.2", revision.VersionTag);
        Assert.Equal(25565, Assert.Single(revision.Ports).ContainerPort);
        var setting = Assert.Single(revision.SettingDefinitions);
        var metadata = Assert.IsType<GameServer.API.Dtos.V2.GameTypeSettingMetadataDto>(setting.Metadata);
        var portMapping = Assert.Single(metadata.PortMappings);
        Assert.Equal("Primary", portMapping.MappingRole);
        Assert.Equal("Direct", portMapping.RelationType);
    }

    [Fact]
    public async Task ExportAsync_WhenGameTypeExists_ShouldStripPersistedIds()
    {
        var repository = new Mock<IGameTypeRepository>();
        repository
            .Setup(x => x.GetByKeyAsync("minecraft"))
            .ReturnsAsync(new GameType
            {
                Id = 1,
                Key = "minecraft",
                DisplayName = "Minecraft",
                Type = "docker",
                CurrentRevisionId = 10,
                Revisions =
                [
                    new GameTypeRevision
                    {
                        Id = 10,
                        VersionTag = "latest",
                        ImageReference = "itzg/minecraft-server",
                        Ports = [ new GameTypePort { Id = 100, ContainerPort = 25565, Protocol = "tcp", AdvertisedPort = true, DisplayOrder = 0 } ],
                        SettingDefinitions =
                        [
                            new GameTypeSettingDefinition
                            {
                                Id = 200,
                                SettingKey = "SERVER_PORT",
                                DisplayOrder = 0,
                                Metadata = new GameTypeSettingMetadata
                                {
                                    Id = 300,
                                    DataType = "port",
                                    PortMappings = [ new GameTypeSettingPortMapping { Id = 400, MappingRole = GameTypeSettingPortMappingRole.Primary, RelationType = GameTypeSettingPortRelationType.Direct, TargetContainerPort = 25565, TargetProtocol = "tcp", DisplayOrder = 0 } ]
                                }
                            }
                        ]
                    }
                ]
            });

        var service = new GameTypeQueryService(repository.Object);

        var result = await service.ExportAsync("minecraft");

        Assert.NotNull(result);
        Assert.Equal("minecraft", result.GameType.Key);
        Assert.Equal("latest", result.GameType.CurrentRevisionVersionTag);
        Assert.Equal(25565, Assert.Single(result.GameType.Revisions[0].Ports).ContainerPort);
        Assert.Equal("Primary", Assert.Single(result.GameType.Revisions[0].SettingDefinitions[0].Metadata!.PortMappings).MappingRole);
    }
}
