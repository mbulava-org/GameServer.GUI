using GameServer.Docker.Configurations;
using GameServer.Docker.Controllers.V2;
using GameServer.Docker.Dtos.V2;
using GameServer.Docker.Interfaces;
using GameServerModel = GameServer.Docker.Models.V2.GameServer;
using GameTypeModel = GameServer.Docker.Models.V2.GameType;
using GameTypeRevisionModel = GameServer.Docker.Models.V2.GameTypeRevision;
using GameServer.Docker.Repositories.V2;
using GameServer.Docker.Services.V2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GameServer.Docker.Tests.Controllers.V2;

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
        var commandService = new GameServerCommandService(serverRepository.Object, service, validationService, new GameServerSpecBuilder(new GameServer.Docker.Configurations.NetworkOptions()));
        var controller = new GameServersController(service, commandService, Mock.Of<ILogger<GameServersController>>());

        // Act
        var result = await controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<GameServer.Docker.Dtos.V2.GameServerListItemDto>>(okResult.Value);
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
        var commandService = new GameServerCommandService(serverRepository.Object, service, validationService, new GameServerSpecBuilder(new GameServer.Docker.Configurations.NetworkOptions()));
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
        var commandService = new GameServerCommandService(serverRepository.Object, queryService, validationService, new GameServerSpecBuilder(new GameServer.Docker.Configurations.NetworkOptions()));
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
        var commandService = new GameServerCommandService(serverRepository.Object, queryService, validationService, new GameServerSpecBuilder(new GameServer.Docker.Configurations.NetworkOptions()));
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
        var commandService = new GameServerCommandService(serverRepository.Object, queryService, validationService, new GameServerSpecBuilder(new GameServer.Docker.Configurations.NetworkOptions()));
        var controller = new GameServersController(queryService, commandService, Mock.Of<ILogger<GameServersController>>());

        // Act
        var result = await controller.Delete("missing", softDelete: true);

        // Assert
        Assert.IsType<NotFoundResult>(result);
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
            new VolumeSetupResolver(Mock.Of<IMountTypeConfigRepository>(), Mock.Of<GameServer.Docker.Services.V2.MountTypeHandlers.IMountTypeHandlerFactory>(), NullLogger<VolumeSetupResolver>.Instance),
            Mock.Of<IMountTypeConfigRepository>());
    }
}

