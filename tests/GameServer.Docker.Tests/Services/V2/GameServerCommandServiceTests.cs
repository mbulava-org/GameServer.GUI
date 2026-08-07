using GameServer.Docker.Configurations;
using GameServer.Docker.Dtos.V2;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Repositories.V2;
using GameServer.Docker.Services.V2;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using GameServerModel = GameServer.Docker.Models.V2.GameServer;
using GameServerSettingModel = GameServer.Docker.Models.V2.GameServerSetting;
using GameTypeModel = GameServer.Docker.Models.V2.GameType;
using GameTypePortModel = GameServer.Docker.Models.V2.GameTypePort;
using GameTypeRevisionModel = GameServer.Docker.Models.V2.GameTypeRevision;
using GameTypeSettingDefinitionModel = GameServer.Docker.Models.V2.GameTypeSettingDefinition;
using GameTypeSettingMetadataModel = GameServer.Docker.Models.V2.GameTypeSettingMetadata;
using GameTypeSettingPortMappingModel = GameServer.Docker.Models.V2.GameTypeSettingPortMapping;

namespace GameServer.Docker.Tests.Services.V2;

public class GameServerCommandServiceTests
{
    [Fact]
    public async Task CreateAsync_WhenRequestIsValid_ShouldCreateAndReturnDetail()
    {
        // Arrange
        var serverRepository = new Mock<IGameServerRepository>();
        serverRepository
            .Setup(x => x.CreateAsync(It.IsAny<GameServerModel>()))
            .ReturnsAsync((GameServerModel server) => server with { Id = 5 });

        serverRepository
            .Setup(x => x.GetByServerIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string serverId) => new GameServerModel
            {
                Id = 5,
                ServerId = serverId,
                Name = "Minecraft Survival",
                GameTypeRevisionId = 10,
                ServiceName = $"gameserver-{serverId}",
                Status = "Stopped",
                Settings =
                [
                    new GameServerSettingModel { SettingKey = "SERVER_PORT", Value = "25565" }
                ]
            });

        serverRepository
            .Setup(x => x.GetAllAsync(false))
            .ReturnsAsync([]);

        var gameTypeRepository = new Mock<IGameTypeRepository>();
        gameTypeRepository
            .Setup(x => x.GetAllAsync(true))
            .ReturnsAsync([CreateGameType()]);

        var serviceOperations = new Mock<IServiceOperations>();
        serviceOperations
            .Setup(x => x.ListServicesAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var queryService = new GameServerQueryService(serverRepository.Object, gameTypeRepository.Object);
        var validationService = new GameServerValidationService(
            gameTypeRepository.Object,
            serviceOperations.Object,
            new PortAllocation { StartPort = 2000, EndPort = 100000 },
            new VolumeSetupResolver(Mock.Of<IMountTypeConfigRepository>(), Mock.Of<GameServer.Docker.Services.V2.MountTypeHandlers.IMountTypeHandlerFactory>(), NullLogger<VolumeSetupResolver>.Instance),
            Mock.Of<IMountTypeConfigRepository>());
        var commandService = new GameServerCommandService(serverRepository.Object, queryService, validationService, new GameServerSpecBuilder(new GameServer.Docker.Configurations.NetworkOptions()));

        var request = new SaveGameServerRequestDto
        {
            Name = "Minecraft Survival",
            GameTypeRevisionId = 10,
            Settings =
            [
                new GameServerSettingDto { SettingKey = "SERVER_PORT", Value = "25565" }
            ]
        };

        // Act
        var result = await commandService.CreateAsync(request);

        // Assert
        Assert.Equal("Minecraft Survival", result.Name);
        Assert.Equal("Stopped", result.Status);
        Assert.StartsWith("gameserver-", result.ServiceName, StringComparison.Ordinal);
    }

    private static GameTypeModel CreateGameType()
    {
        return new GameTypeModel
        {
            Id = 1,
            Key = "minecraft",
            DisplayName = "Minecraft",
            Revisions =
            [
                new GameTypeRevisionModel
                {
                    Id = 10,
                    VersionTag = "1.21.2",
                    ImageReference = "itzg/minecraft-server",
                    Ports =
                    [
                        new GameTypePortModel
                        {
                            ContainerPort = 25565,
                            Protocol = "tcp",
                            AdvertisedPort = true,
                            DisplayOrder = 0
                        }
                    ],
                    SettingDefinitions =
                    [
                        new GameTypeSettingDefinitionModel
                        {
                            SettingKey = "SERVER_PORT",
                            DefaultValue = "25565",
                            Metadata = new GameTypeSettingMetadataModel
                            {
                                DataType = "port",
                                IsRequired = true,
                                PortMappings =
                                [
                                    new GameTypeSettingPortMappingModel
                                    {
                                        MappingRole = GameServer.Docker.Models.V2.GameTypeSettingPortMappingRole.Primary,
                                        RelationType = GameServer.Docker.Models.V2.GameTypeSettingPortRelationType.Direct,
                                        TargetContainerPort = 25565,
                                        TargetProtocol = "tcp",
                                        IsRequired = true
                                    }
                                ]
                            }
                        }
                    ]
                }
            ]
        };
    }
}

