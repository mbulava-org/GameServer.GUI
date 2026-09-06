using GameServer.API.Controllers.V2;
using GameServer.API.Dtos.V2;
using GameServer.API.Interfaces;
using GameServer.API.Models;
using GameServer.API.Models.V2;
using GameServer.API.Repositories.V2;
using GameServer.API.Services.V2;
using GameServer.API.Services.V2.Detection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace GameServer.API.Tests.Controllers.V2;

public class GameTypesControllerTests
{
    private static (GameTypesController Controller, Mock<IGameTypeRepository> Repository) CreateController()
    {
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
        return (controller, repository);
    }

    [Fact]
    public async Task GetAll_WhenGameTypesExist_ShouldReturnOk()
    {
        var (controller, repository) = CreateController();
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

        var result = await controller.GetAll();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<GameServer.API.Dtos.V2.GameTypeListItemDto>>(okResult.Value);
        Assert.Single(payload);
    }

    [Fact]
    public async Task GetByKey_WhenGameTypeDoesNotExist_ShouldReturnNotFound()
    {
        var (controller, repository) = CreateController();
        repository.Setup(x => x.GetByKeyAsync("missing")).ReturnsAsync((GameType?)null);

        var result = await controller.GetByKey("missing");
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetByKey_WhenGameTypeExists_ShouldReturnOk()
    {
        var (controller, repository) = CreateController();
        repository.Setup(x => x.GetByKeyAsync("minecraft")).ReturnsAsync(new GameType { Key = "minecraft", DisplayName = "Minecraft", Type = "docker" });

        var result = await controller.GetByKey("minecraft");
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var detail = Assert.IsType<GameTypeDetailDto>(okResult.Value);
        Assert.Equal("minecraft", detail.Key);
    }

    [Fact]
    public async Task Create_WhenValid_ReturnsCreatedAtAction()
    {
        var (controller, repository) = CreateController();
        repository.Setup(x => x.GetAllAsync(true)).ReturnsAsync([]);
        repository.Setup(x => x.CreateAsync(It.IsAny<GameType>()))
            .ReturnsAsync((GameType gt) => gt with { Id = 1 });
        repository.Setup(x => x.GetByKeyAsync("valheim"))
            .ReturnsAsync(new GameType { Id = 1, Key = "valheim", DisplayName = "Valheim", Type = "docker" });

        var result = await controller.Create(new SaveGameTypeRequestDto { Key = "valheim", DisplayName = "Valheim", Type = "docker" });
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(controller.GetByKey), created.ActionName);
    }

    [Fact]
    public async Task Create_WhenInvalid_ReturnsBadRequest()
    {
        var (controller, _) = CreateController();
        var result = await controller.Create(null!);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Update_WhenValid_ReturnsOk()
    {
        var (controller, repository) = CreateController();
        repository.Setup(x => x.GetByKeyAsync("valheim"))
            .ReturnsAsync(new GameType { Id = 1, Key = "valheim", DisplayName = "Valheim", Type = "docker" });
        repository.Setup(x => x.UpdateAsync(It.IsAny<GameType>()))
            .ReturnsAsync((GameType gt) => gt);

        var result = await controller.Update("valheim", new SaveGameTypeRequestDto { Key = "valheim", DisplayName = "Valheim Updated", Type = "docker" });
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var detail = Assert.IsType<GameTypeDetailDto>(ok.Value);
        Assert.Equal("Valheim Updated", detail.DisplayName);
    }

    [Fact]
    public async Task Update_WhenNotFound_ReturnsNotFound()
    {
        var (controller, repository) = CreateController();
        repository.Setup(x => x.GetByKeyAsync("missing")).ReturnsAsync((GameType?)null);

        var result = await controller.Update("missing", new SaveGameTypeRequestDto { Key = "missing", DisplayName = "Missing", Type = "docker" });
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Update_WhenInvalid_ReturnsBadRequest()
    {
        var (controller, _) = CreateController();
        var result = await controller.Update("valheim", new SaveGameTypeRequestDto { Key = "diff", DisplayName = "Diff", Type = "docker" });
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Delete_WhenCalled_ShouldReturnNoContent()
    {
        var (controller, repository) = CreateController();
        var result = await controller.Delete("minecraft");

        Assert.IsType<NoContentResult>(result);
        repository.Verify(x => x.DeleteAsync("minecraft"), Times.Once);
    }

    [Fact]
    public async Task Export_WhenGameTypeExists_ShouldReturnOk()
    {
        var (controller, repository) = CreateController();
        repository
            .Setup(x => x.GetByKeyAsync("minecraft"))
            .ReturnsAsync(new GameType
            {
                Key = "minecraft",
                DisplayName = "Minecraft",
                Type = "docker",
                Revisions = [ new GameTypeRevision { VersionTag = "latest", ImageReference = "itzg/minecraft-server" } ]
            });

        var result = await controller.Export("minecraft");

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<PortableGameTypePackageDto>(okResult.Value);
        Assert.Equal("minecraft", payload.GameType.Key);
    }

    [Fact]
    public async Task Export_WhenGameTypeMissing_ReturnsNotFound()
    {
        var (controller, repository) = CreateController();
        repository.Setup(x => x.GetByKeyAsync("missing")).ReturnsAsync((GameType?)null);

        var result = await controller.Export("missing");
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Import_WhenPackageIsValid_ShouldReturnCreatedAtAction()
    {
        var (controller, repository) = CreateController();
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

        var result = await controller.Import(new PortableGameTypePackageDto
        {
            GameType = new PortableGameTypeDto
            {
                Key = "minecraft",
                DisplayName = "Minecraft",
                Type = "docker",
                CurrentRevisionVersionTag = "latest",
                Revisions = [ new PortableGameTypeRevisionDto { VersionTag = "latest", ImageReference = "itzg/minecraft-server", Ports = [ new PortableGameTypePortDto { ContainerPort = 25565, Protocol = "tcp", AdvertisedPort = true, DisplayOrder = 0 } ] } ]
            }
        });

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var payload = Assert.IsType<GameTypeDetailDto>(createdResult.Value);
        Assert.Equal("minecraft", payload.Key);
    }

    [Fact]
    public async Task Import_WhenInvalid_ReturnsBadRequest()
    {
        var (controller, _) = CreateController();
        var result = await controller.Import(new PortableGameTypePackageDto
        {
            GameType = new PortableGameTypeDto { Key = "", DisplayName = "", Type = "" }
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Revisions_AddUpdatePublishSetCurrent_HandleSuccessAndNotFound()
    {
        var (controller, repository) = CreateController();
        repository.Setup(x => x.GetByKeyAsync("valheim")).ReturnsAsync(new GameType
        {
            Id = 1,
            Key = "valheim",
            DisplayName = "Valheim",
            Type = "docker",
            Revisions = [ new GameTypeRevision { Id = 10, VersionTag = "1.0", ImageReference = "valheim:latest", Ports = [], Volumes = [], SettingDefinitions = [], WebHosts = [] } ]
        });
        repository.Setup(x => x.AddRevisionAsync(It.IsAny<string>(), It.IsAny<GameTypeRevision>()))
            .ReturnsAsync((string key, GameTypeRevision rev) => rev with { Id = 11, Ports = [], Volumes = [], SettingDefinitions = [], WebHosts = [] });
        repository.Setup(x => x.UpdateRevisionAsync(It.IsAny<string>(), It.IsAny<GameTypeRevision>()))
            .ReturnsAsync((string key, GameTypeRevision rev) => rev with { Ports = [], Volumes = [], SettingDefinitions = [], WebHosts = [] });
        repository.Setup(x => x.SetCurrentRevisionAsync(It.IsAny<string>(), It.IsAny<int>())).Returns(Task.CompletedTask);
        repository.Setup(x => x.UpdateAsync(It.IsAny<GameType>())).ReturnsAsync((GameType gt) => gt);

        // AddRevision
        var addResult = await controller.AddRevision("valheim", new SaveGameTypeRevisionRequestDto { VersionTag = "2.0", ImageReference = "valheim:2.0" });
        Assert.IsType<CreatedAtActionResult>(addResult.Result);

        // UpdateRevision
        var updateResult = await controller.UpdateRevision("valheim", 10, new SaveGameTypeRevisionRequestDto { VersionTag = "1.0", ImageReference = "valheim:latest-updated" });
        Assert.IsType<OkObjectResult>(updateResult.Result);

        // PublishRevision
        var pubResult = await controller.PublishRevision("valheim", 10, new PublishRevisionRequestDto { SetAsCurrentRevision = true });
        Assert.IsType<OkObjectResult>(pubResult.Result);

        // SetCurrentRevision
        var setCurrentResult = await controller.SetCurrentRevision("valheim", 10);
        Assert.IsType<NoContentResult>(setCurrentResult);

        // Missing key branches
        repository.Setup(x => x.GetByKeyAsync("missing")).ReturnsAsync((GameType?)null);
        repository.Setup(x => x.AddRevisionAsync("missing", It.IsAny<GameTypeRevision>())).ThrowsAsync(new KeyNotFoundException());
        repository.Setup(x => x.UpdateRevisionAsync("missing", It.IsAny<GameTypeRevision>())).ThrowsAsync(new KeyNotFoundException());
        repository.Setup(x => x.SetCurrentRevisionAsync("missing", It.IsAny<int>())).ThrowsAsync(new KeyNotFoundException());
        var missingAdd = await controller.AddRevision("missing", new SaveGameTypeRevisionRequestDto { VersionTag = "1.0", ImageReference = "img" });
        Assert.IsType<NotFoundResult>(missingAdd.Result);

        var missingUpdate = await controller.UpdateRevision("missing", 10, new SaveGameTypeRevisionRequestDto { VersionTag = "1.0", ImageReference = "img" });
        Assert.IsType<NotFoundResult>(missingUpdate.Result);

        var missingPub = await controller.PublishRevision("missing", 10, new PublishRevisionRequestDto());
        Assert.IsType<NotFoundResult>(missingPub.Result);

        var missingSet = await controller.SetCurrentRevision("missing", 10);
        Assert.IsType<NotFoundResult>(missingSet);
    }

    [Fact]
    public async Task ScanTag_WhenRequestDoesNotRequireSavedGameType_ShouldReturnOk()
    {
        var repository = new Mock<IGameTypeRepository>();
        var agentRegistry = new Mock<IAgentRegistry>();
        agentRegistry
            .Setup(x => x.GetHealthyAgents())
            .Returns([
                new NodeAgentEndpoint
                {
                    NodeId = "agent-1",
                    NodeName = "agent-1",
                    InternalUrl = "http://agent-1:8080",
                    IsHealthy = true,
                    IsManagerNode = true
                }
            ]);

        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = System.Net.Http.Json.JsonContent.Create(new
                {
                    RepoDigests = new[] { "itzg/minecraft-server@sha256:test" },
                    EnvironmentVariables = new[] { "SERVER_PORT=25565" },
                    ExposedPorts = new[] { "25565/tcp" },
                    VolumePaths = Array.Empty<string>()
                })
            });

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(httpMessageHandler.Object));

        var service = new GameTypeQueryService(repository.Object);
        var commandService = new GameTypeCommandService(repository.Object);
        var detectionLogger = new Mock<ILogger<GameTypeSetupDetectionService>>().Object;
        var controllerLogger = new Mock<ILogger<GameTypesController>>().Object;
        var detectionService = new GameTypeSetupDetectionService(
            repository.Object,
            agentRegistry.Object,
            httpClientFactory.Object,
            detectionLogger);
        var controller = new GameTypesController(service, commandService, detectionService, controllerLogger);

        var result = await controller.ScanTag(new DetectGameTypeSetupRequestDto
        {
            ImageReference = "itzg/minecraft-server",
            VersionTag = "latest"
        });

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<GameTypeSetupDetectionResultDto>(okResult.Value);
        Assert.Equal("itzg/minecraft-server", payload.ImageReference);
    }
}
