using Docker.DotNet.Models;
using GameServer.API.Configurations;
using GameServer.API.Dtos.V2;
using GameServer.API.Interfaces;
using GameServer.API.Repositories.V2;
using GameServer.API.Services.V2;
using GameServer.API.Services.V2.MountTypeHandlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using GameServerModel = GameServer.API.Models.V2.GameServer;
using GameServerSettingModel = GameServer.API.Models.V2.GameServerSetting;
using GameTypeModel = GameServer.API.Models.V2.GameType;
using GameTypePortModel = GameServer.API.Models.V2.GameTypePort;
using GameTypeRevisionModel = GameServer.API.Models.V2.GameTypeRevision;
using GameTypeSettingDefinitionModel = GameServer.API.Models.V2.GameTypeSettingDefinition;
using GameTypeSettingMetadataModel = GameServer.API.Models.V2.GameTypeSettingMetadata;

namespace GameServer.API.Tests.Services.V2;

public class GameServerDeploymentServiceTests
{
    [Fact]
    public void HasSpecChanged_WhenSpecsAreIdentical_ShouldReturnFalse()
    {
        var spec1 = CreateSampleSpec("val1", "gameserver_overlay", "traefik.enable", "true");
        var spec2 = CreateSampleSpec("val1", "gameserver_overlay", "traefik.enable", "true");

        var changed = GameServerDeploymentService.HasSpecChanged(spec1, spec2);

        Assert.False(changed);
    }

    [Fact]
    public void HasSpecChanged_WhenLabelsDiffer_ShouldReturnTrue()
    {
        var spec1 = CreateSampleSpec("val1", "gameserver_overlay", "traefik.enable", "true");
        var spec2 = CreateSampleSpec("val1", "gameserver_overlay", "traefik.enable", "false");

        var changed = GameServerDeploymentService.HasSpecChanged(spec1, spec2);

        Assert.True(changed);
    }

    [Fact]
    public void HasSpecChanged_WhenNetworksDiffer_ShouldReturnTrue()
    {
        var spec1 = CreateSampleSpec("val1", "gameserver_overlay");
        var spec2 = CreateSampleSpec("val1", "traefik_proxy");

        var changed = GameServerDeploymentService.HasSpecChanged(spec1, spec2);

        Assert.True(changed);
    }

    [Fact]
    public void HasSpecChanged_WhenEnvDiffers_ShouldReturnTrue()
    {
        var spec1 = CreateSampleSpec("val1", "gameserver_overlay");
        var spec2 = CreateSampleSpec("val2", "gameserver_overlay");

        var changed = GameServerDeploymentService.HasSpecChanged(spec1, spec2);

        Assert.True(changed);
    }

    [Fact]
    public async Task UpdateDeploymentAsync_WhenSpecMatchesExisting_ShouldSkipDockerUpdate()
    {
        // Arrange
        var serverId = "test-srv-1";
        var gameType = CreateGameType();
        var server = new GameServerModel
        {
            Id = 1,
            ServerId = serverId,
            Name = "Test Server",
            ServiceName = $"gameserver-{serverId}",
            GameTypeRevisionId = 10,
            Status = "Running",
            LastDeployedAt = DateTime.UtcNow.AddHours(-1),
            Settings = [new GameServerSettingModel { SettingKey = "MOTD", Value = "Welcome" }],
            Ports = [new GameServer.API.Models.V2.GameServerPort { ContainerPort = 25565, Protocol = "tcp", PublishedPort = 25565 }]
        };

        var serverRepo = new Mock<IGameServerRepository>();
        serverRepo.Setup(x => x.GetByServerIdAsync(serverId)).ReturnsAsync(server);
        serverRepo.Setup(x => x.GetAllAsync(false)).ReturnsAsync([server]);

        var gameTypeRepo = new Mock<IGameTypeRepository>();
        gameTypeRepo.Setup(x => x.GetAllAsync(true)).ReturnsAsync([gameType]);

        var serviceOperations = new Mock<IServiceOperations>();
        serviceOperations
            .Setup(x => x.ListServicesAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var mountTypeConfigRepo = Mock.Of<IMountTypeConfigRepository>();
        var mountTypeHandlerFactory = new Mock<IMountTypeHandlerFactory>();
        var volumeResolver = new VolumeSetupResolver(mountTypeConfigRepo, mountTypeHandlerFactory.Object, NullLogger<VolumeSetupResolver>.Instance);
        var validationService = new GameServerValidationService(
            gameTypeRepo.Object,
            serviceOperations.Object,
            new PortAllocation { StartPort = 2000, EndPort = 100000 },
            volumeResolver,
            mountTypeConfigRepo);

        var specBuilder = new GameServerSpecBuilder(new NetworkOptions());

        // Generate the exact desired spec that specBuilder would generate
        var saveRequest = new SaveGameServerRequestDto
        {
            ServerId = server.ServerId,
            Name = server.Name,
            GameTypeRevisionId = server.GameTypeRevisionId,
            ServiceName = server.ServiceName,
            Status = server.Status,
            VolumeBindingLayout = "standard",
            Ports = [new GameServerPortDto { ContainerPort = 25565, Protocol = "tcp", PublishedPort = 25565 }],
            Settings = [new GameServerSettingDto { SettingKey = "MOTD", Value = "Welcome" }]
        };
        var resolution = await validationService.ResolveAsync(saveRequest);
        var desiredParams = specBuilder.BuildCreateParameters(saveRequest, resolution);

        // Mock existing Docker service with the matching spec
        serviceOperations
            .Setup(x => x.ListServicesAsync(null, server.ServiceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new SwarmService
                {
                    ID = "srv-123",
                    Spec = desiredParams.Service
                }
            ]);

        var deploymentService = new GameServerDeploymentService(
            serverRepo.Object,
            gameTypeRepo.Object,
            volumeResolver,
            mountTypeHandlerFactory.Object,
            serviceOperations.Object,
            validationService,
            specBuilder,
            NullLogger<GameServerDeploymentService>.Instance);

        // Act
        await deploymentService.UpdateDeploymentAsync(serverId);

        // Assert: UpdateServiceAsync should NOT have been called since spec matches exactly
        serviceOperations.Verify(
            x => x.UpdateServiceAsync(It.IsAny<string>(), It.IsAny<ServiceUpdateParameters>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateDeploymentAsync_WhenSpecDiffers_ShouldInvokeUpdateServiceAsync()
    {
        // Arrange
        var serverId = "test-srv-2";
        var gameType = CreateGameType();
        var server = new GameServerModel
        {
            Id = 2,
            ServerId = serverId,
            Name = "Test Server 2",
            ServiceName = $"gameserver-{serverId}",
            GameTypeRevisionId = 10,
            Status = "Running",
            LastDeployedAt = DateTime.UtcNow.AddHours(-1),
            Settings = [new GameServerSettingModel { SettingKey = "MOTD", Value = "Updated MOTD" }],
            Ports = [new GameServer.API.Models.V2.GameServerPort { ContainerPort = 25565, Protocol = "tcp", PublishedPort = 25565 }]
        };

        var serverRepo = new Mock<IGameServerRepository>();
        serverRepo.Setup(x => x.GetByServerIdAsync(serverId)).ReturnsAsync(server);
        serverRepo.Setup(x => x.GetAllAsync(false)).ReturnsAsync([server]);

        var gameTypeRepo = new Mock<IGameTypeRepository>();
        gameTypeRepo.Setup(x => x.GetAllAsync(true)).ReturnsAsync([gameType]);

        var serviceOperations = new Mock<IServiceOperations>();
        serviceOperations
            .Setup(x => x.ListServicesAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var mountTypeConfigRepo = Mock.Of<IMountTypeConfigRepository>();
        var mountTypeHandlerFactory = new Mock<IMountTypeHandlerFactory>();
        var volumeResolver = new VolumeSetupResolver(mountTypeConfigRepo, mountTypeHandlerFactory.Object, NullLogger<VolumeSetupResolver>.Instance);
        var validationService = new GameServerValidationService(
            gameTypeRepo.Object,
            serviceOperations.Object,
            new PortAllocation { StartPort = 2000, EndPort = 100000 },
            volumeResolver,
            mountTypeConfigRepo);

        var specBuilder = new GameServerSpecBuilder(new NetworkOptions());

        // Existing spec has old MOTD
        var existingSpec = new ServiceSpec
        {
            Name = server.ServiceName,
            TaskTemplate = new TaskSpec
            {
                ContainerSpec = new ContainerSpec
                {
                    Image = "itzg/minecraft-server:latest",
                    Env = ["MOTD=Old MOTD"]
                },
                Networks = [new NetworkAttachmentConfig { Target = "gameserver_overlay" }]
            }
        };

        serviceOperations
            .Setup(x => x.ListServicesAsync(null, server.ServiceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new SwarmService
                {
                    ID = "srv-456",
                    Spec = existingSpec
                }
            ]);

        var deploymentService = new GameServerDeploymentService(
            serverRepo.Object,
            gameTypeRepo.Object,
            volumeResolver,
            mountTypeHandlerFactory.Object,
            serviceOperations.Object,
            validationService,
            specBuilder,
            NullLogger<GameServerDeploymentService>.Instance);

        // Act
        await deploymentService.UpdateDeploymentAsync(serverId);

        // Assert: UpdateServiceAsync should have been called with the new spec
        serviceOperations.Verify(
            x => x.UpdateServiceAsync(server.ServiceName, It.IsAny<ServiceUpdateParameters>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static ServiceSpec CreateSampleSpec(string envVal, string networkName, string? labelKey = null, string? labelVal = null)
    {
        var labels = new Dictionary<string, string>();
        if (labelKey != null && labelVal != null)
        {
            labels[labelKey] = labelVal;
        }

        return new ServiceSpec
        {
            Name = "sample-service",
            Labels = labels,
            TaskTemplate = new TaskSpec
            {
                ContainerSpec = new ContainerSpec
                {
                    Image = "itzg/minecraft-server:latest",
                    Labels = labels,
                    Env = [$"VAR={envVal}"],
                    TTY = true
                },
                Networks = [new NetworkAttachmentConfig { Target = networkName }]
            },
            EndpointSpec = new EndpointSpec
            {
                Ports =
                [
                    new PortConfig
                    {
                        Protocol = "tcp",
                        TargetPort = 25565,
                        PublishedPort = 25565,
                        PublishMode = "ingress"
                    }
                ]
            }
        };
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
                    VersionTag = "latest",
                    ImageReference = "itzg/minecraft-server",
                    EnableTTY = true,
                    SettingDefinitions =
                    [
                        new GameTypeSettingDefinitionModel
                        {
                            SettingKey = "MOTD",
                            DefaultValue = "A Minecraft Server",
                            Metadata = new GameTypeSettingMetadataModel { DataType = "string" }
                        }
                    ],
                    Ports =
                    [
                        new GameTypePortModel
                        {
                            ContainerPort = 25565,
                            Protocol = "tcp",
                            AdvertisedPort = true
                        }
                    ]
                }
            ]
        };
    }
}
