using GameServer.API.Configurations;
using GameServer.API.Dtos.V2;
using GameServer.API.Interfaces;
using GameServer.API.Repositories.V2;
using GameServer.API.Services.V2;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using GameServerModel = GameServer.API.Models.V2.GameServer;
using GameServerSettingModel = GameServer.API.Models.V2.GameServerSetting;
using GameTypeModel = GameServer.API.Models.V2.GameType;
using GameTypePortModel = GameServer.API.Models.V2.GameTypePort;
using GameTypeRevisionModel = GameServer.API.Models.V2.GameTypeRevision;
using GameTypeSettingDefinitionModel = GameServer.API.Models.V2.GameTypeSettingDefinition;
using GameTypeSettingMetadataModel = GameServer.API.Models.V2.GameTypeSettingMetadata;
using GameTypeSettingPortMappingModel = GameServer.API.Models.V2.GameTypeSettingPortMapping;

namespace GameServer.API.Tests.Services.V2;

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

        var mountTypeConfigRepo = Mock.Of<IMountTypeConfigRepository>();
        var mountTypeHandlerFactory = new Mock<GameServer.API.Services.V2.MountTypeHandlers.IMountTypeHandlerFactory>();
        var mountTypeHandler = new Mock<GameServer.API.Services.V2.MountTypeHandlers.IMountTypeHandler>();
        mountTypeHandlerFactory.Setup(x => x.GetHandler(It.IsAny<string>())).Returns(mountTypeHandler.Object);
        mountTypeHandler.Setup(x => x.BuildMount(It.IsAny<GameServer.API.Models.V2.GameServerVolume>())).Returns(new Docker.DotNet.Models.Mount());

        serviceOperations
            .Setup(x => x.CreateServiceAsync(It.IsAny<Docker.DotNet.Models.ServiceCreateParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Docker.DotNet.Models.ServiceCreateResponse { ID = "srv-service-123" });

        var volumeResolver = new VolumeSetupResolver(mountTypeConfigRepo, mountTypeHandlerFactory.Object, NullLogger<VolumeSetupResolver>.Instance);
        var queryService = new GameServerQueryService(serverRepository.Object, gameTypeRepository.Object);
        var validationService = new GameServerValidationService(
            gameTypeRepository.Object,
            serviceOperations.Object,
            new PortAllocation { StartPort = 2000, EndPort = 100000 },
            volumeResolver,
            mountTypeConfigRepo);
        var specBuilder = new GameServerSpecBuilder(new NetworkOptions());
        var deploymentService = new GameServerDeploymentService(
            serverRepository.Object,
            gameTypeRepository.Object,
            volumeResolver,
            mountTypeHandlerFactory.Object,
            serviceOperations.Object,
            validationService,
            specBuilder,
            NullLogger<GameServerDeploymentService>.Instance);
        var commandService = new GameServerCommandService(serverRepository.Object, queryService, validationService, specBuilder, deploymentService);

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
        Assert.StartsWith("gameserver-", result.ServiceName, StringComparison.Ordinal);
        serviceOperations.Verify(x => x.CreateServiceAsync(It.IsAny<Docker.DotNet.Models.ServiceCreateParameters>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenServerExists_ShouldCallRepositoryDelete()
    {
        // Arrange
        var serverRepository = new Mock<IGameServerRepository>();
        serverRepository
            .Setup(x => x.GetByServerIdAsync("srv-1"))
            .ReturnsAsync(new GameServerModel
            {
                Id = 5,
                ServerId = "srv-1",
                Name = "Minecraft Survival"
            });

        var gameTypeRepository = new Mock<IGameTypeRepository>();
        var serviceOperations = new Mock<IServiceOperations>();
        var mountTypeConfigRepo = Mock.Of<IMountTypeConfigRepository>();
        var mountTypeHandlerFactory = new Mock<GameServer.API.Services.V2.MountTypeHandlers.IMountTypeHandlerFactory>();
        var volumeResolver = new VolumeSetupResolver(mountTypeConfigRepo, mountTypeHandlerFactory.Object, NullLogger<VolumeSetupResolver>.Instance);
        var queryService = new GameServerQueryService(serverRepository.Object, gameTypeRepository.Object);
        var validationService = new GameServerValidationService(
            gameTypeRepository.Object,
            serviceOperations.Object,
            new PortAllocation { StartPort = 2000, EndPort = 100000 },
            volumeResolver,
            mountTypeConfigRepo);
        var specBuilder = new GameServerSpecBuilder(new NetworkOptions());
        var deploymentService = new GameServerDeploymentService(
            serverRepository.Object,
            gameTypeRepository.Object,
            volumeResolver,
            mountTypeHandlerFactory.Object,
            serviceOperations.Object,
            validationService,
            specBuilder,
            NullLogger<GameServerDeploymentService>.Instance);
        var commandService = new GameServerCommandService(serverRepository.Object, queryService, validationService, specBuilder, deploymentService);

        // Act
        await commandService.DeleteAsync("srv-1", softDelete: true);

        // Assert
        serverRepository.Verify(x => x.DeleteAsync("srv-1", true), Times.Once);
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
                                        MappingRole = GameServer.API.Models.V2.GameTypeSettingPortMappingRole.Primary,
                                        RelationType = GameServer.API.Models.V2.GameTypeSettingPortRelationType.Direct,
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

