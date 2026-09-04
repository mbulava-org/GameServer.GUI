using Bunit;
using GameServer.Web.Components.Pages.Servers;
using GameServer.Web.Models.V2;
using GameServer.Web.Services;
using GameServer.Web.Services.V2;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Radzen;
using Radzen.Blazor;

namespace GameServer.Web.Tests.Components.Servers.V2;

/// <summary>
/// Editor-level coverage that mocks the V2 API service interfaces directly instead of
/// routing through a stubbed <see cref="HttpMessageHandler"/>.
/// </summary>
public sealed class GameServerEditorV2Tests : BunitContext
{
    private const int PrimaryContainerPort = 25565;
    private const int RelatedContainerPort = 25575;
    private const int RelatedOffset = 100;

    private readonly Mock<IGameServerV2ApiService> gameServerApi = new(MockBehavior.Strict);
    private readonly Mock<IGameTypeV2ApiService> gameTypeApi = new(MockBehavior.Strict);
    private readonly Mock<IMountTypeConfigApiService> mountTypeApi = new(MockBehavior.Strict);

    public GameServerEditorV2Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        gameTypeApi
            .Setup(api => api.GetListAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new GameTypeListItem { Key = "minecraft", DisplayName = "Minecraft", CurrentRevisionId = 10 }]);

        gameTypeApi
            .Setup(api => api.GetByKeyAsync("minecraft", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGameTypeDetail());

        gameServerApi
            .Setup(api => api.GetByServerIdAsync("srv-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateServerDetail());

        mountTypeApi
            .Setup(api => api.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        gameServerApi
            .Setup(api => api.ValidateAsync(It.IsAny<SaveGameServerRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameServerValidationResult { IsValid = true, Issues = [] });

        gameServerApi
            .Setup(api => api.PreviewAsync(It.IsAny<SaveGameServerRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameServerDeploymentPreview { ServiceName = "minecraft-srv-1", Issues = [], Notices = [] });

        SetupAvailability(isAvailable: true);

        Services.AddSingleton<NotificationService>();
        Services.AddSingleton<IThumbnailCacheService>(new PassthroughThumbnailCacheService());
        Services.AddSingleton<IGameServerV2ApiService>(gameServerApi.Object);
        Services.AddSingleton<IGameTypeV2ApiService>(gameTypeApi.Object);
        Services.AddSingleton<IMountTypeConfigApiService>(mountTypeApi.Object);
    }

    [Fact]
    public void GameServerEditorV2_WhenLoaded_ShouldDeriveRelatedPortFromPrimary()
    {
        // Act
        var cut = RenderEditor();

        // Assert
        cut.WaitForAssertion(() =>
        {
            var portInputs = FindPortMappingInputs(cut);
            Assert.Equal(PrimaryContainerPort.ToString(), portInputs[0].GetAttribute("value"));
            Assert.Equal((PrimaryContainerPort + RelatedOffset).ToString(), portInputs[1].GetAttribute("value"));
        });
    }

    [Fact]
    public void GameServerEditorV2_WhenPrimaryPortChanged_ShouldRecalculateRelatedPortAndSyncSetting()
    {
        // Arrange
        SetupAvailability(isAvailable: true);
        var cut = RenderEditor();
        cut.WaitForAssertion(() => Assert.NotEmpty(FindPortMappingInputs(cut)));

        // Act
        FindPortMappingInputs(cut)[0].Change("26000");

        // Assert
        cut.WaitForAssertion(() =>
        {
            var portInputs = FindPortMappingInputs(cut);
            Assert.Equal("26000", portInputs[0].GetAttribute("value"));
            Assert.Equal((26000 + RelatedOffset).ToString(), portInputs[1].GetAttribute("value"));

            // The owning port-type setting is synchronized so save/preview use the edited port.
            Assert.Equal("26000", FindSettingPortInput(cut).GetAttribute("value"));
            Assert.Contains("Port 26000/tcp is available.", cut.Markup);
        });
    }

    [Fact]
    public void GameServerEditorV2_WhenPortUnavailable_ShouldShowReasonAndBlockSaving()
    {
        // Arrange
        SetupAvailability(isAvailable: false, reason: "Port '26000/tcp' is already used by srv-2.");
        var cut = RenderEditor();
        cut.WaitForAssertion(() => Assert.NotEmpty(FindPortMappingInputs(cut)));

        // Act
        FindPortMappingInputs(cut)[0].Change("26000");

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Port '26000/tcp' is already used by srv-2.", cut.Markup);

            var saveButton = cut.FindAll("button").First(button => button.TextContent.Contains("Save Changes"));
            Assert.True(saveButton.HasAttribute("disabled"));
        });
    }

    [Fact]
    public void GameServerEditorV2_WhenDefaultPortUnavailable_ShouldImmediatelyShowUnavailableAndBlockSaving()
    {
        // Arrange
        SetupAvailability(isAvailable: false, reason: "Port '25565/tcp' is already in use by another managed server.");

        // Act
        var cut = RenderEditor();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Port '25565/tcp' is already in use by another managed server.", cut.Markup);

            var saveButton = cut.FindAll("button").First(button => button.TextContent.Contains("Save Changes"));
            Assert.True(saveButton.HasAttribute("disabled"));
        });
    }

    [Fact]
    public void GameServerEditorV2_WhenServerNameUpdated_ShouldTriggerLiveValidation()
    {
        // Arrange
        var cut = RenderEditor();
        cut.WaitForAssertion(() => Assert.NotEmpty(FindPortMappingInputs(cut)));

        // Act
        var nameInput = cut.Find("input.rz-textbox");
        nameInput.Input("My Renamed Server");

        // Assert
        cut.WaitForAssertion(() =>
        {
            gameServerApi.Verify(
                api => api.ValidateAsync(It.Is<SaveGameServerRequest>(req => req.Name == "My Renamed Server"), It.IsAny<CancellationToken>()),
                Times.AtLeastOnce());
        });
    }

    [Fact]
    public void GameServerEditorV2_WhenCreatingServerAndAdvertisedPortUnavailable_ShouldAutoSuggestAvailablePorts()
    {
        // Arrange
        // Port 25565 is unavailable, 25566 and its offset 25666 are available
        gameServerApi
            .Setup(api => api.CheckPortAvailabilityAsync(It.IsAny<GameServerPortAvailabilityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameServerPortAvailabilityRequest request, CancellationToken _) => new GameServerPortAvailabilityResult
            {
                Ports = request.Ports
                    .Select(port => new GameServerPortAvailability
                    {
                        PortId = port.PortId,
                        Port = port.Port,
                        Protocol = port.Protocol,
                        IsAvailable = port.Port != 25565,
                        Reason = port.Port == 25565 ? "Port '25565/tcp' is already in use by another managed server." : null
                    })
                    .ToList()
            });

        // Act: Render create editor (no ServerId)
        var cut = Render<GameServerEditorV2>();
        cut.WaitForAssertion(() => Assert.Contains("Select a game type...", cut.Markup));

        var dropDown = cut.FindComponent<RadzenDropDown<string>>();
        dropDown.InvokeAsync(() => dropDown.Instance.SelectItem("minecraft"));

        // Assert: Advertised port should be suggested as 25566 and related offset as 25666
        cut.WaitForAssertion(() =>
        {
            var portInputs = FindPortMappingInputs(cut);
            Assert.Equal("25566", portInputs[0].GetAttribute("value"));
            Assert.Equal((25566 + RelatedOffset).ToString(), portInputs[1].GetAttribute("value"));
            Assert.Equal("25566", FindSettingPortInput(cut).GetAttribute("value"));
            Assert.Contains("Port 25566/tcp is available.", cut.Markup);
        });
    }

    [Fact]
    public void GameServerEditorV2_WhenCreatingServerAndRelatedPortUnavailable_ShouldAutoSuggestBasePortWhereBothAvailable()
    {
        // Arrange
        // Base port 25565 is free, but related port 25665 is unavailable (in use/reserved)
        // Next candidate base 25566 + related 25666 are both free
        gameServerApi
            .Setup(api => api.CheckPortAvailabilityAsync(It.IsAny<GameServerPortAvailabilityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameServerPortAvailabilityRequest request, CancellationToken _) => new GameServerPortAvailabilityResult
            {
                Ports = request.Ports
                    .Select(port => new GameServerPortAvailability
                    {
                        PortId = port.PortId,
                        Port = port.Port,
                        Protocol = port.Protocol,
                        IsAvailable = port.Port != 25665,
                        Reason = port.Port == 25665 ? "Port '25665/tcp' is in the reserved ports range." : null
                    })
                    .ToList()
            });

        // Act
        var cut = Render<GameServerEditorV2>();
        cut.WaitForAssertion(() => Assert.Contains("Select a game type...", cut.Markup));

        var dropDown = cut.FindComponent<RadzenDropDown<string>>();
        dropDown.InvokeAsync(() => dropDown.Instance.SelectItem("minecraft"));

        // Assert: Because 25665 was unavailable, base port 25565 was skipped and 25566 was chosen
        cut.WaitForAssertion(() =>
        {
            var portInputs = FindPortMappingInputs(cut);
            Assert.Equal("25566", portInputs[0].GetAttribute("value"));
            Assert.Equal((25566 + RelatedOffset).ToString(), portInputs[1].GetAttribute("value"));
            Assert.Equal("25566", FindSettingPortInput(cut).GetAttribute("value"));
        });
    }

    private IRenderedComponent<GameServerEditorV2> RenderEditor()
    {
        return Render<GameServerEditorV2>(parameters => parameters.Add(p => p.ServerId, "srv-1"));
    }

    private void SetupAvailability(bool isAvailable, string? reason = null)
    {
        gameServerApi
            .Setup(api => api.CheckPortAvailabilityAsync(It.IsAny<GameServerPortAvailabilityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameServerPortAvailabilityRequest request, CancellationToken _) => new GameServerPortAvailabilityResult
            {
                Ports = request.Ports
                    .Select(port => new GameServerPortAvailability
                    {
                        PortId = port.PortId,
                        Port = port.Port,
                        Protocol = port.Protocol,
                        IsAvailable = isAvailable,
                        Reason = isAvailable ? null : reason
                    })
                    .ToList()
            });
    }

    private static IReadOnlyList<AngleSharp.Dom.IElement> FindPortMappingInputs(IRenderedComponent<GameServerEditorV2> cut)
    {
        // The Port Mappings card renders before the tab strip, so its numeric inputs come first.
        return cut.FindAll("input.rz-numeric-input").Take(2).ToList();
    }

    private static AngleSharp.Dom.IElement FindSettingPortInput(IRenderedComponent<GameServerEditorV2> cut)
    {
        return cut.FindAll("input.rz-numeric-input")[2];
    }

    private static GameTypeDetail CreateGameTypeDetail()
    {
        return new GameTypeDetail
        {
            Id = 1,
            Key = "minecraft",
            DisplayName = "Minecraft",
            CurrentRevisionId = 10,
            Revisions =
            [
                new GameTypeRevision
                {
                    Id = 10,
                    VersionTag = "1.21.2",
                    ImageReference = "itzg/minecraft-server",
                    Ports =
                    [
                        new GameTypePort { Id = 1, ContainerPort = PrimaryContainerPort, Protocol = "tcp", DisplayOrder = 0 },
                        new GameTypePort { Id = 2, ContainerPort = RelatedContainerPort, Protocol = "tcp", DisplayOrder = 1 }
                    ],
                    SettingDefinitions =
                    [
                        new GameTypeSettingDefinition
                        {
                            Id = 1,
                            SettingKey = "SERVER_PORT",
                            DefaultValue = PrimaryContainerPort.ToString(),
                            Metadata = new GameTypeSettingMetadata
                            {
                                DataType = "port",
                                Category = "Network",
                                IsRequired = true,
                                PortMappings =
                                [
                                    new GameTypeSettingPortMapping
                                    {
                                        Id = 1,
                                        MappingRole = "Primary",
                                        RelationType = "Direct",
                                        TargetContainerPort = PrimaryContainerPort,
                                        TargetProtocol = "tcp"
                                    },
                                    new GameTypeSettingPortMapping
                                    {
                                        Id = 2,
                                        MappingRole = "Related",
                                        RelationType = "Offset",
                                        TargetContainerPort = RelatedContainerPort,
                                        TargetProtocol = "tcp",
                                        CalculationValue = RelatedOffset
                                    }
                                ]
                            }
                        }
                    ]
                }
            ]
        };
    }

    private static GameServerDetail CreateServerDetail()
    {
        return new GameServerDetail
        {
            ServerId = "srv-1",
            Name = "Minecraft Survival",
            GameTypeKey = "minecraft",
            GameTypeRevisionId = 10,
            GameTypeDisplayName = "Minecraft",
            ServiceName = "minecraft-srv-1",
            Status = "Stopped",
            Settings = [new GameServerSetting { SettingKey = "SERVER_PORT", Value = PrimaryContainerPort.ToString() }]
        };
    }

    private sealed class PassthroughThumbnailCacheService : IThumbnailCacheService
    {
        public Task<string?> GetCachedThumbnailUrlAsync(string? sourceUrl, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(sourceUrl);
        }
    }
}
