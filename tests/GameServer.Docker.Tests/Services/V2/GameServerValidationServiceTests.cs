using Docker.DotNet.Models;
using GameServer.Docker.Configurations;
using GameServer.Docker.Constants;
using GameServer.Docker.Dtos.V2;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Models.V2;
using GameServer.Docker.Repositories.V2;
using GameServer.Docker.Services.V2;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GameServer.Docker.Tests.Services.V2;

public class GameServerValidationServiceTests
{
    [Fact]
    public async Task ValidateAsync_WhenRequiredSettingMissing_ShouldReturnBlockingIssue()
    {
        // Arrange
        var service = CreateService(CreateRevision(), services: []);
        var request = new SaveGameServerRequestDto
        {
            Name = "Minecraft Survival",
            GameTypeRevisionId = 10,
            Settings = []
        };

        // Act
        var result = await service.ValidateAsync(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "SettingRequired");
    }

    [Fact]
    public async Task ValidateAsync_WhenPortIsUsedByAnotherServer_ShouldReturnPortUnavailableIssue()
    {
        // Arrange
        var service = CreateService(
            CreateRevision(),
            [
                new SwarmService
                {
                    Spec = new ServiceSpec
                    {
                        Labels = new Dictionary<string, string>
                        {
                            [ServiceLabels.Managed] = ServiceLabels.ManagedValue,
                            [ServiceLabels.ServerId] = "other-server"
                        }
                    },
                    Endpoint = new Endpoint
                    {
                        Ports =
                        [
                            new PortConfig
                            {
                                Protocol = "tcp",
                                TargetPort = 25565,
                                PublishedPort = 25565
                            }
                        ]
                    }
                }
            ]);

        var request = new SaveGameServerRequestDto
        {
            Name = "Minecraft Survival",
            GameTypeRevisionId = 10,
            Settings =
            [
                new GameServerSettingDto
                {
                    SettingKey = "SERVER_PORT",
                    Value = "25565"
                }
            ]
        };

        // Act
        var result = await service.ValidateAsync(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "PortUnavailable");
    }

    private static GameServerValidationService CreateService(GameType gameType, IList<SwarmService> services)
    {
        var gameTypeRepository = new Mock<IGameTypeRepository>();
        gameTypeRepository
            .Setup(x => x.GetAllAsync(true))
            .ReturnsAsync([gameType]);

        var serviceOperations = new Mock<IServiceOperations>();
        serviceOperations
            .Setup(x => x.ListServicesAsync($"{ServiceLabels.Managed}={ServiceLabels.ManagedValue}", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        return new GameServerValidationService(
            gameTypeRepository.Object,
            serviceOperations.Object,
            new PortAllocation { StartPort = 2000, EndPort = 100000 },
            new VolumeSetupResolver(Mock.Of<IMountTypeConfigRepository>(), NullLogger<VolumeSetupResolver>.Instance));
    }

    private static GameType CreateRevision()
    {
        return new GameType
        {
            Id = 1,
            Key = "minecraft",
            DisplayName = "Minecraft",
            Revisions =
            [
                new GameTypeRevision
                {
                    Id = 10,
                    VersionTag = "1.21.2",
                    ImageReference = "itzg/minecraft-server",
                    Ports =
                    [
                        new GameTypePort
                        {
                            ContainerPort = 25565,
                            Protocol = "tcp",
                            AdvertisedPort = true,
                            DisplayOrder = 0
                        }
                    ],
                    SettingDefinitions =
                    [
                        new GameTypeSettingDefinition
                        {
                            SettingKey = "SERVER_PORT",
                            Metadata = new GameTypeSettingMetadata
                            {
                                DataType = "port",
                                IsRequired = true,
                                PortMappings =
                                [
                                    new GameTypeSettingPortMapping
                                    {
                                        MappingRole = GameTypeSettingPortMappingRole.Primary,
                                        RelationType = GameTypeSettingPortRelationType.Direct,
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
