using GameServer.Docker.Controllers.V2;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Models.V2;
using GameServer.Docker.Repositories.V2;
using GameServer.Docker.Services.V2;
using GameServer.Docker.Services.V2.Detection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace GameServer.Docker.Tests.Controllers.V2;

public class GameTypesControllerTests
{
    [Fact]
    public async Task GetAll_WhenGameTypesExist_ShouldReturnOk()
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
                    Revisions =
                    [
                        new GameTypeRevision
                        {
                            Id = 1,
                            ImageReference = "itzg/minecraft-server",
                            VersionTag = "latest"
                        }
                    ]
                }
            ]);

        var service = new GameTypeQueryService(repository.Object);
        var commandService = new GameTypeCommandService(repository.Object);
        var detectionLogger = new Mock<ILogger<GameTypeSetupDetectionService>>().Object;
        var controllerLogger = new Mock<ILogger<GameTypesController>>().Object;
        var detectionService = new GameTypeSetupDetectionService(
            repository.Object,
            Mock.Of<IAgentRegistry>(),
            Mock.Of<IHttpClientFactory>(),
            detectionLogger);
        var controller = new GameTypesController(service, commandService, detectionService, controllerLogger);

        // Act
        var result = await controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<GameServer.Docker.Dtos.V2.GameTypeListItemDto>>(okResult.Value);
        Assert.Single(payload);
    }

    [Fact]
    public async Task GetByKey_WhenGameTypeDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        var repository = new Mock<IGameTypeRepository>();
        repository
            .Setup(x => x.GetByKeyAsync("missing"))
            .ReturnsAsync((GameType?)null);

        var service = new GameTypeQueryService(repository.Object);
        var commandService = new GameTypeCommandService(repository.Object);
        var detectionLogger = new Mock<ILogger<GameTypeSetupDetectionService>>().Object;
        var controllerLogger = new Mock<ILogger<GameTypesController>>().Object;
        var detectionService = new GameTypeSetupDetectionService(
            repository.Object,
            Mock.Of<IAgentRegistry>(),
            Mock.Of<IHttpClientFactory>(),
            detectionLogger);
        var controller = new GameTypesController(service, commandService, detectionService, controllerLogger);

        // Act
        var result = await controller.GetByKey("missing");

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Delete_WhenCalled_ShouldReturnNoContent()
    {
        // Arrange
        var repository = new Mock<IGameTypeRepository>();

        var service = new GameTypeQueryService(repository.Object);
        var commandService = new GameTypeCommandService(repository.Object);
        var detectionLogger = new Mock<ILogger<GameTypeSetupDetectionService>>().Object;
        var controllerLogger = new Mock<ILogger<GameTypesController>>().Object;
        var detectionService = new GameTypeSetupDetectionService(
            repository.Object,
            Mock.Of<IAgentRegistry>(),
            Mock.Of<IHttpClientFactory>(),
            detectionLogger);
        var controller = new GameTypesController(service, commandService, detectionService, controllerLogger);

        // Act
        var result = await controller.Delete("minecraft");

        // Assert
        Assert.IsType<NoContentResult>(result);
        repository.Verify(x => x.DeleteAsync("minecraft"), Times.Once);
    }

    [Fact]
    public async Task Export_WhenGameTypeExists_ShouldReturnOk()
    {
        var repository = new Mock<IGameTypeRepository>();
        repository
            .Setup(x => x.GetByKeyAsync("minecraft"))
            .ReturnsAsync(new GameType
            {
                Key = "minecraft",
                DisplayName = "Minecraft",
                Type = "docker",
                Revisions = [ new GameTypeRevision { VersionTag = "latest", ImageReference = "itzg/minecraft-server" } ]
            });

        var service = new GameTypeQueryService(repository.Object);
        var commandService = new GameTypeCommandService(repository.Object);
        var detectionLogger = new Mock<ILogger<GameTypeSetupDetectionService>>().Object;
        var controllerLogger = new Mock<ILogger<GameTypesController>>().Object;
        var detectionService = new GameTypeSetupDetectionService(
            repository.Object,
            Mock.Of<IAgentRegistry>(),
            Mock.Of<IHttpClientFactory>(),
            detectionLogger);
        var controller = new GameTypesController(service, commandService, detectionService, controllerLogger);

        var result = await controller.Export("minecraft");

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<GameServer.Docker.Dtos.V2.PortableGameTypePackageDto>(okResult.Value);
        Assert.Equal("minecraft", payload.GameType.Key);
    }

    [Fact]
    public async Task Import_WhenPackageIsValid_ShouldReturnCreatedAtAction()
    {
        var repository = new Mock<IGameTypeRepository>();
        repository
            .Setup(x => x.CreateAsync(It.IsAny<GameType>()))
            .ReturnsAsync((GameType gameType) => gameType with
            {
                Id = 1,
                Revisions = gameType.Revisions.Select((revision, index) => revision with { Id = index + 10 }).ToList()
            });
        repository
            .Setup(x => x.GetByKeyAsync("minecraft"))
            .ReturnsAsync(new GameType
            {
                Id = 1,
                Key = "minecraft",
                DisplayName = "Minecraft",
                Type = "docker",
                CurrentRevisionId = 10,
                Revisions = [ new GameTypeRevision { Id = 10, VersionTag = "latest", ImageReference = "itzg/minecraft-server" } ]
            });

        var service = new GameTypeQueryService(repository.Object);
        var commandService = new GameTypeCommandService(repository.Object);
        var detectionLogger = new Mock<ILogger<GameTypeSetupDetectionService>>().Object;
        var controllerLogger = new Mock<ILogger<GameTypesController>>().Object;
        var detectionService = new GameTypeSetupDetectionService(
            repository.Object,
            Mock.Of<IAgentRegistry>(),
            Mock.Of<IHttpClientFactory>(),
            detectionLogger);
        var controller = new GameTypesController(service, commandService, detectionService, controllerLogger);

        var result = await controller.Import(new GameServer.Docker.Dtos.V2.PortableGameTypePackageDto
        {
            GameType = new GameServer.Docker.Dtos.V2.PortableGameTypeDto
            {
                Key = "minecraft",
                DisplayName = "Minecraft",
                Type = "docker",
                CurrentRevisionVersionTag = "latest",
                Revisions = [ new GameServer.Docker.Dtos.V2.PortableGameTypeRevisionDto { VersionTag = "latest", ImageReference = "itzg/minecraft-server", Ports = [ new GameServer.Docker.Dtos.V2.PortableGameTypePortDto { ContainerPort = 25565, Protocol = "tcp", AdvertisedPort = true, DisplayOrder = 0 } ] } ]
            }
        });

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var payload = Assert.IsType<GameServer.Docker.Dtos.V2.GameTypeDetailDto>(createdResult.Value);
        Assert.Equal("minecraft", payload.Key);
    }
}
