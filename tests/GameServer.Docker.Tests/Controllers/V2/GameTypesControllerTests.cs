using GameServer.Docker.Controllers.V2;
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
                    ImageReference = "itzg/minecraft-server"
                }
            ]);

        var service = new GameTypeQueryService(repository.Object);
        var commandService = new GameTypeCommandService(repository.Object);
        var detectionLogger = new Mock<ILogger<GameTypeSetupDetectionService>>().Object;
        var controllerLogger = new Mock<ILogger<GameTypesController>>().Object;
        var detectionService = new GameTypeSetupDetectionService(repository.Object, null, detectionLogger);
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
        var detectionService = new GameTypeSetupDetectionService(repository.Object, null, detectionLogger);
        var controller = new GameTypesController(service, commandService, detectionService, controllerLogger);

        // Act
        var result = await controller.GetByKey("missing");

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }
}
