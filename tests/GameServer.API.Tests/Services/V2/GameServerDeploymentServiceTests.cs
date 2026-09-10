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
    public void HasSpecChanged_WhenDNSConfigDiffers_ShouldReturnTrue()
    {
        var spec1 = CreateSampleSpec("val1", "gameserver_overlay");
        var spec2 = CreateSampleSpec("val1", "gameserver_overlay");
        spec2.TaskTemplate.ContainerSpec.DNSConfig = new DNSConfig
        {
            Nameservers = new List<string> { "1.1.1.1" }
        };

        var changed = GameServerDeploymentService.HasSpecChanged(spec1, spec2);

        Assert.True(changed);
    }

    [Fact]
    public void HasSpecChanged_WhenUserDiffers_ShouldReturnTrue()
    {
        var spec1 = CreateSampleSpec("val1", "gameserver_overlay");
        var spec2 = CreateSampleSpec("val1", "gameserver_overlay");
        spec2.TaskTemplate.ContainerSpec.User = "1001:1001";

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

    [Fact]
    public async Task DeployAsync_WhenCreatingNewService_ShouldSetStatusToPreparing()
    {
        // Arrange
        var serverId = "deploy-srv";
        var gameType = CreateGameType();
        var server = new GameServerModel
        {
            Id = 3,
            ServerId = serverId,
            Name = "Deploy Server",
            ServiceName = $"gameserver-{serverId}",
            GameTypeRevisionId = 10,
            Status = "Stopped",
            Settings = [new GameServerSettingModel { SettingKey = "MOTD", Value = "Welcome" }],
            Ports = [new GameServer.API.Models.V2.GameServerPort { ContainerPort = 25565, Protocol = "tcp", PublishedPort = 25565 }]
        };

        var serverRepo = new Mock<IGameServerRepository>();
        serverRepo.Setup(x => x.GetByServerIdAsync(serverId)).ReturnsAsync(server);
        serverRepo.Setup(x => x.GetAllAsync(It.IsAny<bool>())).ReturnsAsync([server]);

        var gameTypeRepo = new Mock<IGameTypeRepository>();
        gameTypeRepo.Setup(x => x.GetAllAsync(true)).ReturnsAsync([gameType]);

        var serviceOperations = new Mock<IServiceOperations>();
        serviceOperations
            .Setup(x => x.ListServicesAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        serviceOperations
            .Setup(x => x.CreateServiceAsync(It.IsAny<ServiceCreateParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceCreateResponse { ID = "srv-new-123" });

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

        GameServerModel? updatedServer = null;
        serverRepo.Setup(x => x.UpdateAsync(It.IsAny<GameServerModel>()))
            .Callback<GameServerModel>(s => updatedServer = s)
            .ReturnsAsync((GameServerModel s) => s);

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
        await deploymentService.DeployAsync(serverId);

        // Assert
        Assert.NotNull(updatedServer);
        Assert.Equal("Preparing", updatedServer.Status);
    }

    [Fact]
    public async Task StartAsync_WhenScalingExistingService_ShouldSetStatusToStarting()
    {
        // Arrange
        var serverId = "start-srv";
        var gameType = CreateGameType();
        var server = new GameServerModel
        {
            Id = 4,
            ServerId = serverId,
            Name = "Start Server",
            ServiceName = $"gameserver-{serverId}",
            GameTypeRevisionId = 10,
            Status = "Stopped",
            Settings = [],
            Ports = []
        };

        var serverRepo = new Mock<IGameServerRepository>();
        serverRepo.Setup(x => x.GetByServerIdAsync(serverId)).ReturnsAsync(server);

        var gameTypeRepo = new Mock<IGameTypeRepository>();
        gameTypeRepo.Setup(x => x.GetAllAsync(true)).ReturnsAsync([gameType]);

        var serviceOperations = new Mock<IServiceOperations>();
        serviceOperations
            .Setup(x => x.ListServicesAsync(null, server.ServiceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new SwarmService { ID = "srv-start-123", Spec = new ServiceSpec { Name = server.ServiceName } }]);

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

        GameServerModel? updatedServer = null;
        serverRepo.Setup(x => x.UpdateAsync(It.IsAny<GameServerModel>()))
            .Callback<GameServerModel>(s => updatedServer = s)
            .ReturnsAsync((GameServerModel s) => s);

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
        await deploymentService.StartAsync(serverId);

        // Assert
        Assert.NotNull(updatedServer);
        Assert.Equal("Starting", updatedServer.Status);
    }

    [Fact]
    public async Task StopAsync_WhenScalingToZeroReplicas_ShouldSetStatusToStopped()
    {
        // Arrange
        var serverId = "stop-srv";
        var gameType = CreateGameType();
        var server = new GameServerModel
        {
            Id = 5,
            ServerId = serverId,
            Name = "Stop Server",
            ServiceName = $"gameserver-{serverId}",
            GameTypeRevisionId = 10,
            Status = "Running",
            Settings = [],
            Ports = []
        };

        var serverRepo = new Mock<IGameServerRepository>();
        serverRepo.Setup(x => x.GetByServerIdAsync(serverId)).ReturnsAsync(server);

        var gameTypeRepo = new Mock<IGameTypeRepository>();

        var serviceOperations = new Mock<IServiceOperations>();
        serviceOperations
            .Setup(x => x.ListServicesAsync(null, server.ServiceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new SwarmService { ID = "srv-stop-123", Spec = new ServiceSpec { Name = server.ServiceName } }]);

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

        GameServerModel? updatedServer = null;
        serverRepo.Setup(x => x.UpdateAsync(It.IsAny<GameServerModel>()))
            .Callback<GameServerModel>(s => updatedServer = s)
            .ReturnsAsync((GameServerModel s) => s);

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
        await deploymentService.StopAsync(serverId);

        // Assert
        Assert.NotNull(updatedServer);
        Assert.Equal("Stopped", updatedServer.Status);
    }

    [Fact]
    public async Task DeployAsync_WhenWindowsAskaServerConfigured_ShouldInstallFilesAndWriteServerProperties()
    {
        var serverId = "aska-srv";
        var gameType = CreateAskaGameType();
        var server = new GameServerModel
        {
            Id = 6,
            ServerId = serverId,
            Name = "Aska Server",
            ServiceName = $"gameserver-{serverId}",
            GameTypeRevisionId = 30,
            Status = "Stopped",
            Settings =
            [
                new GameServerSettingModel { SettingKey = "ASKA_AUTHENTICATION_TOKEN", Value = "gslt-token-123" }
            ],
            Ports =
            [
                new GameServer.API.Models.V2.GameServerPort { ContainerPort = 7777, Protocol = "udp", PublishedPort = 7777 },
                new GameServer.API.Models.V2.GameServerPort { ContainerPort = 27015, Protocol = "udp", PublishedPort = 27015 }
            ]
        };

        var serverRepo = new Mock<IGameServerRepository>();
        serverRepo.Setup(x => x.GetByServerIdAsync(serverId)).ReturnsAsync(server);
        GameServerModel? updatedServer = null;
        serverRepo.Setup(x => x.UpdateAsync(It.IsAny<GameServerModel>()))
            .Callback<GameServerModel>(model => updatedServer = model)
            .ReturnsAsync((GameServerModel model) => model);

        var gameTypeRepo = new Mock<IGameTypeRepository>();
        gameTypeRepo.Setup(x => x.GetAllAsync(true)).ReturnsAsync([gameType]);

        var serviceOperations = new Mock<IServiceOperations>(MockBehavior.Strict);
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

        var windowsAgentOperations = new Mock<IWindowsAgentOperations>();
        WindowsSteamAppInstallRequest? installRequest = null;
        WindowsStartServerRequest? startRequest = null;
        windowsAgentOperations.Setup(x => x.InstallOrUpdateSteamAppAsync("http://windows-agent/", It.IsAny<WindowsSteamAppInstallRequest>(), It.IsAny<CancellationToken>()))
            .Callback<string, WindowsSteamAppInstallRequest, CancellationToken>((_, request, _) => installRequest = request)
            .ReturnsAsync(new WindowsSteamCmdJobResult { Success = true, AppId = 3246670, ExitCode = 0, Message = "ok" });
        windowsAgentOperations.Setup(x => x.StartServerAsync("http://windows-agent/", It.IsAny<WindowsStartServerRequest>(), It.IsAny<CancellationToken>()))
            .Callback<string, WindowsStartServerRequest, CancellationToken>((_, request, _) => startRequest = request)
            .ReturnsAsync(new WindowsProcessInfo { ServerId = serverId, Status = "Running" });

        var nodeAgentDiscovery = new Mock<INodeAgentDiscovery>();
        nodeAgentDiscovery.Setup(x => x.GetAgentForServerAsync(serverId))
            .ReturnsAsync(new GameServer.API.Models.NodeAgentEndpoint
            {
                InternalUrl = "http://windows-agent/",
                HostType = "windows",
                IsHealthy = true
            });

        var deploymentService = new GameServerDeploymentService(
            serverRepo.Object,
            gameTypeRepo.Object,
            volumeResolver,
            mountTypeHandlerFactory.Object,
            serviceOperations.Object,
            validationService,
            specBuilder,
            NullLogger<GameServerDeploymentService>.Instance,
            windowsAgentOperations: windowsAgentOperations.Object,
            nodeAgentDiscovery: nodeAgentDiscovery.Object);

        await deploymentService.DeployAsync(serverId);

        Assert.NotNull(installRequest);
        Assert.Equal(3246670u, installRequest.AppId);
        Assert.True(installRequest.AnonymousLogin);

        Assert.NotNull(startRequest);
        Assert.Equal("AskaServer.exe", startRequest.ExecutablePath);
        Assert.Equal("-propertiesPath \"server properties.txt\"", startRequest.Arguments);
        Assert.Contains("BepInEx/plugins", startRequest.DirectoriesToEnsure);
        var serverProperties = Assert.Single(startRequest.TextFilesToWrite);
        Assert.Equal("server properties.txt", serverProperties.RelativePath);
        Assert.Contains("authentication token = gslt-token-123", serverProperties.Content);
        Assert.Contains("steam game port = 7777", serverProperties.Content);
        Assert.Contains("steam query port = 27015", serverProperties.Content);
        Assert.DoesNotContain("ASKA_AUTHENTICATION_TOKEN", startRequest.EnvironmentVariables!.Keys);
        Assert.NotNull(updatedServer);
        Assert.Equal("Starting", updatedServer.Status);
    }

    [Fact]
    public async Task DeployAsync_WhenWindowsAskaServerMissingAuthToken_ShouldThrow()
    {
        var serverId = "aska-srv";
        var gameType = CreateAskaGameType();
        var server = new GameServerModel
        {
            Id = 7,
            ServerId = serverId,
            Name = "Aska Server",
            ServiceName = $"gameserver-{serverId}",
            GameTypeRevisionId = 30,
            Status = "Stopped",
            Settings = [],
            Ports =
            [
                new GameServer.API.Models.V2.GameServerPort { ContainerPort = 7777, Protocol = "udp", PublishedPort = 7777 },
                new GameServer.API.Models.V2.GameServerPort { ContainerPort = 27015, Protocol = "udp", PublishedPort = 27015 }
            ]
        };

        var serverRepo = new Mock<IGameServerRepository>();
        serverRepo.Setup(x => x.GetByServerIdAsync(serverId)).ReturnsAsync(server);

        var gameTypeRepo = new Mock<IGameTypeRepository>();
        gameTypeRepo.Setup(x => x.GetAllAsync(true)).ReturnsAsync([gameType]);

        var serviceOperations = new Mock<IServiceOperations>(MockBehavior.Strict);
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

        var windowsAgentOperations = new Mock<IWindowsAgentOperations>();
        var nodeAgentDiscovery = new Mock<INodeAgentDiscovery>();
        nodeAgentDiscovery.Setup(x => x.GetAgentForServerAsync(serverId))
            .ReturnsAsync(new GameServer.API.Models.NodeAgentEndpoint
            {
                InternalUrl = "http://windows-agent/",
                HostType = "windows",
                IsHealthy = true
            });

        var deploymentService = new GameServerDeploymentService(
            serverRepo.Object,
            gameTypeRepo.Object,
            volumeResolver,
            mountTypeHandlerFactory.Object,
            serviceOperations.Object,
            validationService,
            specBuilder,
            NullLogger<GameServerDeploymentService>.Instance,
            windowsAgentOperations: windowsAgentOperations.Object,
            nodeAgentDiscovery: nodeAgentDiscovery.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => deploymentService.DeployAsync(serverId));
        Assert.Contains("ASKA_AUTHENTICATION_TOKEN", ex.Message);
        windowsAgentOperations.Verify(x => x.InstallOrUpdateSteamAppAsync(It.IsAny<string>(), It.IsAny<WindowsSteamAppInstallRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        windowsAgentOperations.Verify(x => x.StartServerAsync(It.IsAny<string>(), It.IsAny<WindowsStartServerRequest>(), It.IsAny<CancellationToken>()), Times.Never);
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

    private static GameTypeModel CreateAskaGameType()
    {
        return new GameTypeModel
        {
            Id = 2,
            Key = "aska-dedicated",
            DisplayName = "ASKA Dedicated Server",
            Type = "windows",
            Revisions =
            [
                new GameTypeRevisionModel
                {
                    Id = 30,
                    VersionTag = "latest",
                    ImageReference = "AskaServer.exe",
                    SettingDefinitions =
                    [
                        new GameTypeSettingDefinitionModel { SettingKey = "STEAMCMD_APP_ID", DefaultValue = "3246670", Metadata = new GameTypeSettingMetadataModel { DataType = "number" } },
                        new GameTypeSettingDefinitionModel { SettingKey = "STEAMCMD_ANONYMOUS_LOGIN", DefaultValue = "true", Metadata = new GameTypeSettingMetadataModel { DataType = "boolean" } },
                        new GameTypeSettingDefinitionModel { SettingKey = "ASKA_DISPLAY_NAME", DefaultValue = "My ASKA Server", Metadata = new GameTypeSettingMetadataModel { DataType = "string" } },
                        new GameTypeSettingDefinitionModel { SettingKey = "ASKA_SERVER_NAME", DefaultValue = "aska-server", Metadata = new GameTypeSettingMetadataModel { DataType = "string" } },
                        new GameTypeSettingDefinitionModel { SettingKey = "ASKA_MODE", DefaultValue = "normal", Metadata = new GameTypeSettingMetadataModel { DataType = "string" } },
                        new GameTypeSettingDefinitionModel { SettingKey = "ASKA_REGION", DefaultValue = "default", Metadata = new GameTypeSettingMetadataModel { DataType = "string" } },
                        new GameTypeSettingDefinitionModel { SettingKey = "ASKA_KEEP_SERVER_WORLD_ALIVE", DefaultValue = "true", Metadata = new GameTypeSettingMetadataModel { DataType = "boolean" } },
                        new GameTypeSettingDefinitionModel { SettingKey = "ASKA_AUTOSAVE_STYLE", DefaultValue = "every 10 minutes", Metadata = new GameTypeSettingMetadataModel { DataType = "string" } },
                        new GameTypeSettingDefinitionModel { SettingKey = "ASKA_STEAM_GAME_PORT", DefaultValue = "7777", Metadata = new GameTypeSettingMetadataModel { DataType = "port" } },
                        new GameTypeSettingDefinitionModel { SettingKey = "ASKA_STEAM_QUERY_PORT", DefaultValue = "27015", Metadata = new GameTypeSettingMetadataModel { DataType = "port" } }
                    ],
                    Ports =
                    [
                        new GameTypePortModel
                        {
                            ContainerPort = 7777,
                            Protocol = "udp",
                            AdvertisedPort = true
                        },
                        new GameTypePortModel
                        {
                            ContainerPort = 27015,
                            Protocol = "udp",
                            AdvertisedPort = false
                        }
                    ]
                }
            ]
        };
    }
}
