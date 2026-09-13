using GameServer.API.Configurations;
using GameServer.API.Dtos.V2;
using GameServer.API.Interfaces;
using GameServer.API.Models.V2;
using GameServer.API.Repositories.V2;
using GameServer.API.Services.V2;
using GameServer.API.Services.V2.MountTypeHandlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using GameServerModel = GameServer.API.Models.V2.GameServer;
using GameTypeModel = GameServer.API.Models.V2.GameType;
using GameTypeRevisionModel = GameServer.API.Models.V2.GameTypeRevision;

namespace GameServer.API.Tests.Services.V2;

public sealed class ContainerImageUpdateServiceTests
{
    private readonly Mock<IGameServerRepository> _serverRepoMock = new();
    private readonly Mock<IGameTypeRepository> _gameTypeRepoMock = new();
    private readonly Mock<IDockerRegistryService> _registryServiceMock = new();
    private readonly Mock<IServiceOperations> _serviceOperationsMock = new();
    private readonly Mock<IVolumeSetupResolver> _volumeSetupResolverMock = new();
    private readonly Mock<IMountTypeHandlerFactory> _mountTypeHandlerFactoryMock = new();

    private ContainerImageUpdateService CreateService()
    {
        var gameType = new GameTypeModel
        {
            Id = 1,
            Key = "valheim",
            DisplayName = "Valheim",
            Revisions =
            [
                new GameTypeRevisionModel
                {
                    Id = 10,
                    VersionTag = "latest",
                    ImageReference = "lloesche/valheim-server",
                    ImageDigest = "sha256:1111111111111111111111111111111111111111111111111111111111111111"
                }
            ]
        };

        _gameTypeRepoMock.Setup(g => g.GetAllAsync(true))
            .ReturnsAsync([gameType]);

        var mountTypeConfigRepo = Mock.Of<IMountTypeConfigRepository>();
        var validationService = new GameServerValidationService(
            _gameTypeRepoMock.Object,
            _serviceOperationsMock.Object,
            new PortAllocation(),
            _volumeSetupResolverMock.Object,
            mountTypeConfigRepo);

        var specBuilder = new GameServerSpecBuilder(new NetworkOptions());

        var deploymentService = new GameServerDeploymentService(
            _serverRepoMock.Object,
            _gameTypeRepoMock.Object,
            _volumeSetupResolverMock.Object,
            _mountTypeHandlerFactoryMock.Object,
            _serviceOperationsMock.Object,
            validationService,
            specBuilder,
            NullLogger<GameServerDeploymentService>.Instance);

        var queryService = new GameServerQueryService(
            _serverRepoMock.Object,
            _gameTypeRepoMock.Object);

        return new ContainerImageUpdateService(
            _serverRepoMock.Object,
            _gameTypeRepoMock.Object,
            _registryServiceMock.Object,
            deploymentService,
            queryService,
            NullLogger<ContainerImageUpdateService>.Instance);
    }

    [Fact]
    public async Task CheckImageUpdateAsync_WhenRemoteDigestDiffersFromCurrent_ShouldReturnUpdateAvailable()
    {
        // Arrange
        var server = new GameServerModel
        {
            ServerId = "srv-valheim",
            Name = "Valheim Server",
            GameTypeRevisionId = 10,
            ServiceName = "gameserver-srv-valheim",
            Status = "Running"
        };

        _serverRepoMock.Setup(r => r.GetByServerIdAsync("srv-valheim"))
            .ReturnsAsync(server);

        // Current digest in revision is sha256:1111...
        // Remote digest from registry is sha256:2222... (new release)
        _registryServiceMock.Setup(r => r.GetRemoteDigestAsync("lloesche/valheim-server", "latest", It.IsAny<CancellationToken>()))
            .ReturnsAsync("sha256:2222222222222222222222222222222222222222222222222222222222222222");

        var service = CreateService();

        // Act
        var result = await service.CheckImageUpdateAsync("srv-valheim");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsUpdateAvailable);
        Assert.Equal("sha256:1111111111111111111111111111111111111111111111111111111111111111", result.CurrentDigest);
        Assert.Equal("sha256:2222222222222222222222222222222222222222222222222222222222222222", result.LatestDigest);
    }

    [Fact]
    public async Task CheckImageUpdateAsync_WhenRemoteDigestMatchesCurrent_ShouldReturnNoUpdate()
    {
        // Arrange
        var server = new GameServerModel
        {
            ServerId = "srv-valheim",
            Name = "Valheim Server",
            GameTypeRevisionId = 10,
            ServiceName = "gameserver-srv-valheim",
            Status = "Running"
        };

        _serverRepoMock.Setup(r => r.GetByServerIdAsync("srv-valheim"))
            .ReturnsAsync(server);

        // Remote digest matches current
        _registryServiceMock.Setup(r => r.GetRemoteDigestAsync("lloesche/valheim-server", "latest", It.IsAny<CancellationToken>()))
            .ReturnsAsync("sha256:1111111111111111111111111111111111111111111111111111111111111111");

        var service = CreateService();

        // Act
        var result = await service.CheckImageUpdateAsync("srv-valheim");

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsUpdateAvailable);
        Assert.Equal(result.CurrentDigest, result.LatestDigest);
    }
}
