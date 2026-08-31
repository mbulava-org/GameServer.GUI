using GameServer.API.Configurations;
using GameServer.API.Controllers.V2;
using GameServer.API.Dtos.V2;
using GameServer.API.Interfaces;
using GameServerModel = GameServer.API.Models.V2.GameServer;
using GameTypeModel = GameServer.API.Models.V2.GameType;
using GameTypeRevisionModel = GameServer.API.Models.V2.GameTypeRevision;
using GameServer.API.Repositories.V2;
using GameServer.API.Services.V2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GameServer.API.Tests.Controllers.V2;

public class GameServersControllerTests
{
    [Fact]
    public async Task GetAll_WhenServersExist_ShouldReturnOk()
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
                    GameTypeRevisionId = 10,
                    ServiceName = "minecraft-survival",
                    Status = "Running"
                }
            ]);

        var gameTypeRepository = new Mock<IGameTypeRepository>();
        gameTypeRepository
            .Setup(x => x.GetAllAsync(true))
            .ReturnsAsync(
            [
                new GameTypeModel
                {
                    Key = "minecraft",
                    DisplayName = "Minecraft",
                    Revisions = [ new GameTypeRevisionModel { Id = 10, VersionTag = "1.21.2", ImageReference = "itzg/minecraft-server" } ]
                }
            ]);

        var service = new GameServerQueryService(serverRepository.Object, gameTypeRepository.Object);
        var validationService = CreateValidationService(gameTypeRepository);
        var commandService = CreateCommandService(serverRepository, gameTypeRepository, service, validationService);
        var controller = new GameServersController(service, commandService, Mock.Of<ILogger<GameServersController>>());

        // Act
        var result = await controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<GameServer.API.Dtos.V2.GameServerListItemDto>>(okResult.Value);
        Assert.Single(payload);
    }

    [Fact]
    public async Task GetByServerId_WhenServerDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        var serverRepository = new Mock<IGameServerRepository>();
        serverRepository
            .Setup(x => x.GetByServerIdAsync("missing"))
            .ReturnsAsync((GameServerModel?)null);

        var gameTypeRepository = new Mock<IGameTypeRepository>();
        gameTypeRepository
            .Setup(x => x.GetAllAsync(true))
            .ReturnsAsync([]);

        var service = new GameServerQueryService(serverRepository.Object, gameTypeRepository.Object);
        var validationService = CreateValidationService(gameTypeRepository);
        var commandService = CreateCommandService(serverRepository, gameTypeRepository, service, validationService);
        var controller = new GameServersController(service, commandService, Mock.Of<ILogger<GameServersController>>());

        // Act
        var result = await controller.GetByServerId("missing");

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Validate_WhenRequestIsValid_ShouldReturnOk()
    {
        // Arrange
        var serverRepository = new Mock<IGameServerRepository>();
        serverRepository.Setup(x => x.GetAllAsync(false)).ReturnsAsync([]);

        var gameTypeRepository = new Mock<IGameTypeRepository>();
        gameTypeRepository
            .Setup(x => x.GetAllAsync(true))
            .ReturnsAsync(
            [
                new GameTypeModel
                {
                    Key = "minecraft",
                    DisplayName = "Minecraft",
                    Revisions =
                    [
                        new GameTypeRevisionModel
                        {
                            Id = 10,
                            VersionTag = "1.21.2",
                            ImageReference = "itzg/minecraft-server"
                        }
                    ]
                }
            ]);

        var queryService = new GameServerQueryService(serverRepository.Object, gameTypeRepository.Object);
        var validationService = CreateValidationService(gameTypeRepository);
        var commandService = CreateCommandService(serverRepository, gameTypeRepository, queryService, validationService);
        var controller = new GameServersController(queryService, commandService, Mock.Of<ILogger<GameServersController>>());

        // Act
        var result = await controller.Validate(new SaveGameServerRequestDto
        {
            Name = "Minecraft Survival",
            GameTypeRevisionId = 10
        });

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<GameServerValidationResultDto>(okResult.Value);
    }

    [Fact]
    public async Task Delete_WhenServerExists_ShouldReturnNoContent()
    {
        // Arrange
        var serverRepository = new Mock<IGameServerRepository>();
        serverRepository
            .Setup(x => x.GetByServerIdAsync("srv-1"))
            .ReturnsAsync(new GameServerModel { Id = 1, ServerId = "srv-1" });
        serverRepository
            .Setup(x => x.DeleteAsync("srv-1", true))
            .Returns(Task.CompletedTask);

        var gameTypeRepository = new Mock<IGameTypeRepository>();
        var queryService = new GameServerQueryService(serverRepository.Object, gameTypeRepository.Object);
        var validationService = CreateValidationService(gameTypeRepository);
        var commandService = CreateCommandService(serverRepository, gameTypeRepository, queryService, validationService);
        var controller = new GameServersController(queryService, commandService, Mock.Of<ILogger<GameServersController>>());

        // Act
        var result = await controller.Delete("srv-1", softDelete: true);

        // Assert
        Assert.IsType<NoContentResult>(result);
        serverRepository.Verify(x => x.DeleteAsync("srv-1", true), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenServerDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        var serverRepository = new Mock<IGameServerRepository>();
        serverRepository
            .Setup(x => x.GetByServerIdAsync("missing"))
            .ReturnsAsync((GameServerModel?)null);

        var gameTypeRepository = new Mock<IGameTypeRepository>();
        var queryService = new GameServerQueryService(serverRepository.Object, gameTypeRepository.Object);
        var validationService = CreateValidationService(gameTypeRepository);
        var commandService = CreateCommandService(serverRepository, gameTypeRepository, queryService, validationService);
        var controller = new GameServersController(queryService, commandService, Mock.Of<ILogger<GameServersController>>());

        // Act
        var result = await controller.Delete("missing", softDelete: true);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Start_WhenServerDoesNotExist_ShouldReturnNotFound()
    {
        var serverRepository = new Mock<IGameServerRepository>();
        serverRepository.Setup(x => x.GetByServerIdAsync("missing")).ReturnsAsync((GameServerModel?)null);
        var gameTypeRepository = new Mock<IGameTypeRepository>();
        var queryService = new GameServerQueryService(serverRepository.Object, gameTypeRepository.Object);
        var validationService = CreateValidationService(gameTypeRepository);
        var commandService = CreateCommandService(serverRepository, gameTypeRepository, queryService, validationService);
        var controller = new GameServersController(queryService, commandService, Mock.Of<ILogger<GameServersController>>());

        var result = await controller.Start("missing");
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Stop_WhenServerDoesNotExist_ShouldReturnNotFound()
    {
        var serverRepository = new Mock<IGameServerRepository>();
        serverRepository.Setup(x => x.GetByServerIdAsync("missing")).ReturnsAsync((GameServerModel?)null);
        var gameTypeRepository = new Mock<IGameTypeRepository>();
        var queryService = new GameServerQueryService(serverRepository.Object, gameTypeRepository.Object);
        var validationService = CreateValidationService(gameTypeRepository);
        var commandService = CreateCommandService(serverRepository, gameTypeRepository, queryService, validationService);
        var controller = new GameServersController(queryService, commandService, Mock.Of<ILogger<GameServersController>>());

        var result = await controller.Stop("missing");
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Restart_WhenServerDoesNotExist_ShouldReturnNotFound()
    {
        var serverRepository = new Mock<IGameServerRepository>();
        serverRepository.Setup(x => x.GetByServerIdAsync("missing")).ReturnsAsync((GameServerModel?)null);
        var gameTypeRepository = new Mock<IGameTypeRepository>();
        var queryService = new GameServerQueryService(serverRepository.Object, gameTypeRepository.Object);
        var validationService = CreateValidationService(gameTypeRepository);
        var commandService = CreateCommandService(serverRepository, gameTypeRepository, queryService, validationService);
        var controller = new GameServersController(queryService, commandService, Mock.Of<ILogger<GameServersController>>());

        var result = await controller.Restart("missing");
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Redeploy_WhenServerDoesNotExist_ShouldReturnNotFound()
    {
        var serverRepository = new Mock<IGameServerRepository>();
        serverRepository.Setup(x => x.GetByServerIdAsync("missing")).ReturnsAsync((GameServerModel?)null);
        var gameTypeRepository = new Mock<IGameTypeRepository>();
        var queryService = new GameServerQueryService(serverRepository.Object, gameTypeRepository.Object);
        var validationService = CreateValidationService(gameTypeRepository);
        var commandService = CreateCommandService(serverRepository, gameTypeRepository, queryService, validationService);
        var controller = new GameServersController(queryService, commandService, Mock.Of<ILogger<GameServersController>>());

        var result = await controller.Redeploy("missing");
        Assert.IsType<NotFoundResult>(result.Result);
    }

    private static GameServerValidationService CreateValidationService(Mock<IGameTypeRepository> gameTypeRepository)
    {
        var serviceOperations = new Mock<IServiceOperations>();
        serviceOperations
            .Setup(x => x.ListServicesAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        return new GameServerValidationService(
            gameTypeRepository.Object,
            serviceOperations.Object,
            new PortAllocation { StartPort = 2000, EndPort = 100000 },
            new VolumeSetupResolver(Mock.Of<IMountTypeConfigRepository>(), Mock.Of<GameServer.API.Services.V2.MountTypeHandlers.IMountTypeHandlerFactory>(), NullLogger<VolumeSetupResolver>.Instance),
            Mock.Of<IMountTypeConfigRepository>());
    }

    private static GameServerCommandService CreateCommandService(
        Mock<IGameServerRepository> serverRepository,
        Mock<IGameTypeRepository> gameTypeRepository,
        GameServerQueryService queryService,
        GameServerValidationService validationService)
    {
        var specBuilder = new GameServerSpecBuilder(new NetworkOptions());
        var mountTypeConfigRepo = Mock.Of<IMountTypeConfigRepository>();
        var mountTypeHandlerFactory = new Mock<GameServer.API.Services.V2.MountTypeHandlers.IMountTypeHandlerFactory>();
        var volumeResolver = new VolumeSetupResolver(mountTypeConfigRepo, mountTypeHandlerFactory.Object, NullLogger<VolumeSetupResolver>.Instance);
        var serviceOperations = new Mock<IServiceOperations>();
        var deploymentService = new GameServerDeploymentService(
            serverRepository.Object,
            gameTypeRepository.Object,
            volumeResolver,
            mountTypeHandlerFactory.Object,
            serviceOperations.Object,
            validationService,
            specBuilder,
            NullLogger<GameServerDeploymentService>.Instance);

        return new GameServerCommandService(
            serverRepository.Object,
            queryService,
            validationService,
            specBuilder,
            deploymentService);
    }
}

