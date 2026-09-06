using Docker.DotNet.Models;
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
    public async Task GetByServerId_WhenServerExists_ShouldReturnOk()
    {
        var serverRepository = new Mock<IGameServerRepository>();
        serverRepository
            .Setup(x => x.GetByServerIdAsync("srv-1"))
            .ReturnsAsync(new GameServerModel { Id = 1, ServerId = "srv-1", Name = "Server 1", GameTypeRevisionId = 10 });

        var gameTypeRepository = new Mock<IGameTypeRepository>();
        gameTypeRepository
            .Setup(x => x.GetAllAsync(true))
            .ReturnsAsync([ new GameTypeModel { Key = "minecraft", Revisions = [ new GameTypeRevisionModel { Id = 10 } ] } ]);

        var service = new GameServerQueryService(serverRepository.Object, gameTypeRepository.Object);
        var validationService = CreateValidationService(gameTypeRepository);
        var commandService = CreateCommandService(serverRepository, gameTypeRepository, service, validationService);
        var controller = new GameServersController(service, commandService, Mock.Of<ILogger<GameServersController>>());

        var result = await controller.GetByServerId("srv-1");
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var detail = Assert.IsType<GameServerDetailDto>(okResult.Value);
        Assert.Equal("srv-1", detail.ServerId);
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
    public async Task Validate_WhenArgumentExceptionThrown_ReturnsBadRequest()
    {
        var serverRepo = new Mock<IGameServerRepository>();
        var gameTypeRepo = new Mock<IGameTypeRepository>();
        var queryService = new GameServerQueryService(serverRepo.Object, gameTypeRepo.Object);
        var validationService = CreateValidationService(gameTypeRepo);
        var commandService = CreateCommandService(serverRepo, gameTypeRepo, queryService, validationService);
        var controller = new GameServersController(queryService, commandService, Mock.Of<ILogger<GameServersController>>());

        var result = await controller.Validate(new SaveGameServerRequestDto
        {
            Name = "",
            GameTypeRevisionId = 0
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Preview_WhenValid_ReturnsOk()
    {
        var serverRepo = new Mock<IGameServerRepository>();
        var gameTypeRepo = new Mock<IGameTypeRepository>();
        gameTypeRepo.Setup(x => x.GetAllAsync(true)).ReturnsAsync([
            new GameTypeModel { Key = "minecraft", Revisions = [ new GameTypeRevisionModel { Id = 10, ImageReference = "itzg/minecraft" } ] }
        ]);
        var queryService = new GameServerQueryService(serverRepo.Object, gameTypeRepo.Object);
        var validationService = CreateValidationService(gameTypeRepo);
        var commandService = CreateCommandService(serverRepo, gameTypeRepo, queryService, validationService);
        var controller = new GameServersController(queryService, commandService, Mock.Of<ILogger<GameServersController>>());

        var result = await controller.Preview(new SaveGameServerRequestDto
        {
            Name = "Server 1",
            GameTypeRevisionId = 10
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<GameServerDeploymentPreviewDto>(ok.Value);
    }

    [Fact]
    public async Task Preview_WhenInvalid_ReturnsBadRequest()
    {
        var serverRepo = new Mock<IGameServerRepository>();
        var gameTypeRepo = new Mock<IGameTypeRepository>();
        var queryService = new GameServerQueryService(serverRepo.Object, gameTypeRepo.Object);
        var validationService = CreateValidationService(gameTypeRepo);
        var commandService = CreateCommandService(serverRepo, gameTypeRepo, queryService, validationService);
        var controller = new GameServersController(queryService, commandService, Mock.Of<ILogger<GameServersController>>());

        var result = await controller.Preview(new SaveGameServerRequestDto { Name = "", GameTypeRevisionId = 0 });
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task CheckPortAvailability_WhenCalled_ReturnsOk()
    {
        var serverRepo = new Mock<IGameServerRepository>();
        var gameTypeRepo = new Mock<IGameTypeRepository>();
        var queryService = new GameServerQueryService(serverRepo.Object, gameTypeRepo.Object);
        var validationService = CreateValidationService(gameTypeRepo);
        var commandService = CreateCommandService(serverRepo, gameTypeRepo, queryService, validationService);
        var controller = new GameServersController(queryService, commandService, Mock.Of<ILogger<GameServersController>>());

        var result = await controller.CheckPortAvailability(new GameServerPortAvailabilityRequestDto
        {
            Ports = [ new GameServerPortAvailabilityRequestPortDto { PortId = 1, Port = 25565, Protocol = "tcp" } ]
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<GameServerPortAvailabilityResultDto>(ok.Value);
    }

    [Fact]
    public async Task CheckPortAvailability_WhenInvalid_ReturnsBadRequest()
    {
        var serverRepo = new Mock<IGameServerRepository>();
        var gameTypeRepo = new Mock<IGameTypeRepository>();
        var queryService = new GameServerQueryService(serverRepo.Object, gameTypeRepo.Object);
        var validationService = CreateValidationService(gameTypeRepo);
        var commandService = CreateCommandService(serverRepo, gameTypeRepo, queryService, validationService);
        var controller = new GameServersController(queryService, commandService, Mock.Of<ILogger<GameServersController>>());

        var result = await controller.CheckPortAvailability(null!);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_WhenValid_ReturnsCreatedAtAction()
    {
        var serverRepo = new Mock<IGameServerRepository>();
        serverRepo.Setup(x => x.GetAllAsync(false)).ReturnsAsync([]);
        serverRepo.Setup(x => x.CreateAsync(It.IsAny<GameServerModel>()))
            .ReturnsAsync((GameServerModel m) => new GameServerModel
            {
                Id = 1,
                ServerId = "srv-1",
                Name = m.Name,
                GameTypeRevisionId = m.GameTypeRevisionId
            });
        serverRepo.Setup(x => x.GetByServerIdAsync("srv-1"))
            .ReturnsAsync(new GameServerModel { Id = 1, ServerId = "srv-1", Name = "Server 1", GameTypeRevisionId = 10 });

        var gameTypeRepo = new Mock<IGameTypeRepository>();
        gameTypeRepo.Setup(x => x.GetAllAsync(true)).ReturnsAsync([
            new GameTypeModel { Key = "minecraft", Revisions = [ new GameTypeRevisionModel { Id = 10, ImageReference = "itzg/minecraft" } ] }
        ]);

        var queryService = new GameServerQueryService(serverRepo.Object, gameTypeRepo.Object);
        var validationService = CreateValidationService(gameTypeRepo);
        var commandService = CreateCommandService(serverRepo, gameTypeRepo, queryService, validationService);
        var controller = new GameServersController(queryService, commandService, Mock.Of<ILogger<GameServersController>>());

        var result = await controller.Create(new SaveGameServerRequestDto
        {
            Name = "Server 1",
            GameTypeRevisionId = 10
        });

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(controller.GetByServerId), createdResult.ActionName);
    }

    [Fact]
    public async Task Create_WhenInvalid_ReturnsBadRequest()
    {
        var serverRepo = new Mock<IGameServerRepository>();
        var gameTypeRepo = new Mock<IGameTypeRepository>();
        var queryService = new GameServerQueryService(serverRepo.Object, gameTypeRepo.Object);
        var validationService = CreateValidationService(gameTypeRepo);
        var commandService = CreateCommandService(serverRepo, gameTypeRepo, queryService, validationService);
        var controller = new GameServersController(queryService, commandService, Mock.Of<ILogger<GameServersController>>());

        var result = await controller.Create(new SaveGameServerRequestDto { Name = "", GameTypeRevisionId = 0 });
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Update_WhenNotFound_ReturnsNotFound()
    {
        var serverRepo = new Mock<IGameServerRepository>();
        serverRepo.Setup(x => x.GetByServerIdAsync("missing")).ReturnsAsync((GameServerModel?)null);
        var gameTypeRepo = new Mock<IGameTypeRepository>();
        var queryService = new GameServerQueryService(serverRepo.Object, gameTypeRepo.Object);
        var validationService = CreateValidationService(gameTypeRepo);
        var commandService = CreateCommandService(serverRepo, gameTypeRepo, queryService, validationService);
        var controller = new GameServersController(queryService, commandService, Mock.Of<ILogger<GameServersController>>());

        var result = await controller.Update("missing", new SaveGameServerRequestDto { Name = "Updated", GameTypeRevisionId = 10 });
        Assert.IsType<NotFoundResult>(result.Result);
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

    [Fact]
    public async Task GetResourceHistory_WhenRepoNull_ReturnsEmpty()
    {
        var serverRepo = new Mock<IGameServerRepository>();
        var gameTypeRepo = new Mock<IGameTypeRepository>();
        var queryService = new GameServerQueryService(serverRepo.Object, gameTypeRepo.Object);
        var validationService = CreateValidationService(gameTypeRepo);
        var commandService = CreateCommandService(serverRepo, gameTypeRepo, queryService, validationService);
        var controller = new GameServersController(queryService, commandService, Mock.Of<ILogger<GameServersController>>());

        var result = await controller.GetResourceHistory("srv-1");
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<GameServerResourceHistoryDto>>(ok.Value);
        Assert.Empty(list);
    }

    [Fact]
    public async Task GetResourceHistory_WhenRepoInjected_ReturnsRecords()
    {
        var serverRepo = new Mock<IGameServerRepository>();
        var gameTypeRepo = new Mock<IGameTypeRepository>();
        var queryService = new GameServerQueryService(serverRepo.Object, gameTypeRepo.Object);
        var validationService = CreateValidationService(gameTypeRepo);
        var commandService = CreateCommandService(serverRepo, gameTypeRepo, queryService, validationService);
        var resourceRepo = new Mock<IGameServerResourceUtilizationRepository>();
        resourceRepo.Setup(r => r.GetHistoryAsync("srv-1", null, null, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new GameServer.API.Data.V2.GameServerResourceUtilizationEntity
                {
                    Id = 1,
                    ServerId = "srv-1",
                    Timestamp = DateTime.UtcNow,
                    CpuUsagePercent = 10.0,
                    MemoryUsageBytes = 500000000
                }
            ]);

        var controller = new GameServersController(queryService, commandService, Mock.Of<ILogger<GameServersController>>(), resourceRepo.Object);

        var result = await controller.GetResourceHistory("srv-1", limit: 100);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<GameServerResourceHistoryDto>>(ok.Value);
        Assert.Single(list);
        Assert.Equal("srv-1", list[0].ServerId);
    }

    [Fact]
    public async Task GetLatestResource_WhenCachedExists_ReturnsOk()
    {
        var serverRepo = new Mock<IGameServerRepository>();
        var gameTypeRepo = new Mock<IGameTypeRepository>();
        var queryService = new GameServerQueryService(serverRepo.Object, gameTypeRepo.Object);
        var validationService = CreateValidationService(gameTypeRepo);
        var commandService = CreateCommandService(serverRepo, gameTypeRepo, queryService, validationService);
        var collector = new Mock<IGameServerResourceCollector>();
        collector.Setup(c => c.GetCachedUsage("srv-1"))
            .Returns(new GameServer.API.Models.ServerResourceUsage
            {
                ServerId = "srv-1",
                RealTimeStats = new GameServer.API.Models.ContainerStats { CpuUsagePercent = 22.2 }
            });

        var controller = new GameServersController(queryService, commandService, Mock.Of<ILogger<GameServersController>>(), null, collector.Object);

        var result = await controller.GetLatestResource("srv-1");
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var usage = Assert.IsType<GameServer.API.Models.ServerResourceUsage>(ok.Value);
        Assert.Equal(22.2, usage.CpuUsagePercent);
    }

    [Fact]
    public async Task GetLatestResource_WhenMonitorHasSnapshot_ReturnsOk()
    {
        var serverRepo = new Mock<IGameServerRepository>();
        var gameTypeRepo = new Mock<IGameTypeRepository>();
        var queryService = new GameServerQueryService(serverRepo.Object, gameTypeRepo.Object);
        var validationService = CreateValidationService(gameTypeRepo);
        var commandService = CreateCommandService(serverRepo, gameTypeRepo, queryService, validationService);
        var monitor = new Mock<Interfaces.IServerResourceMonitor>();
        monitor.Setup(m => m.GetSnapshotAsync("srv-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameServer.API.Models.ServerResourceUsage
            {
                ServerId = "srv-1",
                RealTimeStats = new GameServer.API.Models.ContainerStats { CpuUsagePercent = 33.3 }
            });

        var controller = new GameServersController(queryService, commandService, Mock.Of<ILogger<GameServersController>>(), null, null, monitor.Object);

        var result = await controller.GetLatestResource("srv-1");
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var usage = Assert.IsType<GameServer.API.Models.ServerResourceUsage>(ok.Value);
        Assert.Equal(33.3, usage.CpuUsagePercent);
    }

    [Fact]
    public async Task GetLatestResource_WhenNoData_ReturnsNotFound()
    {
        var serverRepo = new Mock<IGameServerRepository>();
        var gameTypeRepo = new Mock<IGameTypeRepository>();
        var queryService = new GameServerQueryService(serverRepo.Object, gameTypeRepo.Object);
        var validationService = CreateValidationService(gameTypeRepo);
        var commandService = CreateCommandService(serverRepo, gameTypeRepo, queryService, validationService);

        var controller = new GameServersController(queryService, commandService, Mock.Of<ILogger<GameServersController>>());

        var result = await controller.GetLatestResource("srv-1");
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
        serviceOperations.Setup(x => x.CreateServiceAsync(It.IsAny<ServiceCreateParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceCreateResponse { ID = "svc-1" });
        serviceOperations.Setup(x => x.UpdateServiceAsync(It.IsAny<string>(), It.IsAny<ServiceUpdateParameters>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        serviceOperations.Setup(x => x.RemoveServiceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        serviceOperations.Setup(x => x.ListServicesAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

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
