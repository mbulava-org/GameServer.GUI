using Docker.DotNet.Models;
using GameServer.API.Configurations;
using GameServer.API.Constants;
using GameServer.API.Dtos.V2;
using GameServer.API.Interfaces;
using GameServer.API.Models.V2;
using GameServer.API.Repositories.V2;
using GameServer.API.Services.V2;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GameServer.API.Tests.Services.V2;

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
            new VolumeSetupResolver(Mock.Of<IMountTypeConfigRepository>(), Mock.Of<GameServer.API.Services.V2.MountTypeHandlers.IMountTypeHandlerFactory>(), NullLogger<VolumeSetupResolver>.Instance),
            Mock.Of<IMountTypeConfigRepository>());
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

    [Fact]
    public async Task ResolveAsync_WhenPortSettingHasNoExplicitPortMappings_ShouldResolvePortFromSettingValue()
    {
        var gameType = new GameType
        {
            Id = 1,
            Key = "minecraft",
            Revisions =
            [
                new GameTypeRevision
                {
                    Id = 1,
                    Ports =
                    [
                        new GameTypePort
                        {
                            ContainerPort = 25565,
                            Protocol = "tcp",
                            AdvertisedPort = true,
                            DisplayOrder = 1
                        }
                    ],
                    SettingDefinitions =
                    [
                        new GameTypeSettingDefinition
                        {
                            SettingKey = "SERVER_PORT",
                            DefaultValue = "25565",
                            DisplayOrder = 1,
                            Metadata = new GameTypeSettingMetadata
                            {
                                DataType = "port",
                                IsRequired = true,
                                PortMappings = []
                            }
                        }
                    ]
                }
            ]
        };

        var service = CreateService(gameType, services: []);

        var resolution = await service.ResolveAsync(new SaveGameServerRequestDto
        {
            Name = "Server",
            GameTypeRevisionId = 1,
            Settings = [new GameServerSettingDto { SettingKey = "SERVER_PORT", Value = "26000" }]
        });

        var resolvedPort = Assert.Single(resolution.Result.ResolvedPorts);
        Assert.Equal(26000, resolvedPort.ContainerPort);
        Assert.Equal(26000, resolvedPort.PublishedPort);
    }

    [Fact]
    public async Task ResolveAsync_WhenPortMappingLinkedToEnvironmentVariable_ShouldSetContainerPortAndPublishedPortToDerivedValue()
    {
        var gameType = new GameType
        {
            Id = 1,
            Key = "conan",
            Revisions =
            [
                new GameTypeRevision
                {
                    Id = 1,
                    Ports =
                    [
                        new GameTypePort { ContainerPort = 7777, Protocol = "udp", DisplayOrder = 1 },
                        new GameTypePort { ContainerPort = 7778, Protocol = "udp", DisplayOrder = 2 }
                    ],
                    SettingDefinitions =
                    [
                        new GameTypeSettingDefinition
                        {
                            SettingKey = "SERVER_PORT",
                            DefaultValue = "7777",
                            DisplayOrder = 1,
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
                                        TargetContainerPort = 7777,
                                        TargetProtocol = "udp"
                                    },
                                    new GameTypeSettingPortMapping
                                    {
                                        MappingRole = GameTypeSettingPortMappingRole.Related,
                                        RelationType = GameTypeSettingPortRelationType.Offset,
                                        CalculationValue = 1,
                                        TargetContainerPort = 7778,
                                        TargetProtocol = "udp"
                                    }
                                ]
                            }
                        }
                    ]
                }
            ]
        };

        var service = CreateService(gameType, services: []);

        var resolution = await service.ResolveAsync(new SaveGameServerRequestDto
        {
            Name = "Conan Server",
            GameTypeRevisionId = 1,
            Settings = [new GameServerSettingDto { SettingKey = "SERVER_PORT", Value = "8000" }]
        });

        Assert.Equal(2, resolution.Result.ResolvedPorts.Count);
        var primaryPort = resolution.Result.ResolvedPorts[0];
        Assert.Equal(8000, primaryPort.ContainerPort);
        Assert.Equal(8000, primaryPort.PublishedPort);

        var relatedPort = resolution.Result.ResolvedPorts[1];
        Assert.Equal(8001, relatedPort.ContainerPort);
        Assert.Equal(8001, relatedPort.PublishedPort);
    }

    [Fact]
    public async Task ResolveAsync_WhenRequestContainsExplicitPorts_ShouldResolvePortFromRequestPorts()
    {
        var gameType = new GameType
        {
            Id = 1,
            Key = "minecraft",
            Revisions =
            [
                new GameTypeRevision
                {
                    Id = 1,
                    Ports =
                    [
                        new GameTypePort
                        {
                            ContainerPort = 25565,
                            Protocol = "tcp",
                            AdvertisedPort = true,
                            DisplayOrder = 1
                        }
                    ],
                    SettingDefinitions = []
                }
            ]
        };

        var service = CreateService(gameType, services: []);

        var resolution = await service.ResolveAsync(new SaveGameServerRequestDto
        {
            Name = "Server",
            GameTypeRevisionId = 1,
            Ports =
            [
                new GameServerPortDto
                {
                    ContainerPort = 25565,
                    Protocol = "tcp",
                    PublishedPort = 26500
                }
            ]
        });

        var resolvedPort = Assert.Single(resolution.Result.ResolvedPorts);
        Assert.Equal(26500, resolvedPort.PublishedPort);
    }
}

