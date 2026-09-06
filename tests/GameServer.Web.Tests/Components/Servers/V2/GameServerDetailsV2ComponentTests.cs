using Bunit;
using GameServer.Web.Components.Pages.Servers;
using GameServer.Web.Models.V2;
using GameServer.Web.Services;
using GameServer.Web.Services.V2;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Radzen;

namespace GameServer.Web.Tests.Components.Servers.V2;

public sealed class GameServerDetailsV2ComponentTests : BunitContext
{
    private readonly Mock<IGameServerV2ApiService> serverApi = new();
    private readonly Mock<IGameTypeV2ApiService> gameTypeApi = new();
    private readonly Mock<IThumbnailCacheService> thumbnailCache = new();
    private readonly Mock<IPublicIpService> publicIpService = new();
    private readonly Mock<IGameServerFilesApiService> filesApi = new();
    private readonly Mock<IGameTypeExtensionResolver> extensionResolver = new();

    public GameServerDetailsV2ComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<DialogService>();
        Services.AddSingleton<NotificationService>();
        Services.AddSingleton<TooltipService>();
        Services.AddSingleton(serverApi.Object);
        Services.AddSingleton(gameTypeApi.Object);
        Services.AddSingleton(thumbnailCache.Object);
        Services.AddSingleton(publicIpService.Object);
        Services.AddSingleton(filesApi.Object);
        Services.AddSingleton(extensionResolver.Object);
        publicIpService
            .Setup(p => p.GetPublicIpAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("203.0.113.195");
        thumbnailCache
            .Setup(t => t.GetCachedThumbnailUrlAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string? url, CancellationToken _) => url);
    }

    [Fact]
    public void GameServerDetailsV2_WhenLoaded_ShouldRenderOverviewAndTabs()
    {
        // Arrange
        var server = new GameServerDetail
        {
            ServerId = "srv-1",
            Name = "Valheim Viking World",
            Description = "Dedicated Survival",
            GameTypeDisplayName = "Valheim",
            RevisionVersionTag = "0.217.46",
            ServiceName = "valheim-srv-1",
            Status = "Running",
            Settings =
            [
                new GameServerSetting { SettingKey = "SERVER_NAME", Value = "Viking World" }
            ],
            ResolvedPorts =
            [
                new GameServerResolvedPort { ContainerPort = 2456, Protocol = "udp", DisplayOrder = 0 }
            ],
            ResolvedVolumes =
            [
                new GameServerResolvedVolume { VolumeName = "valheim_data", ContainerPath = "/config", MountType = "volume", ReadOnly = false, IsProvisioned = true }
            ]
        };

        serverApi.Setup(a => a.GetByServerIdAsync("srv-1", It.IsAny<CancellationToken>())).ReturnsAsync(server);
        serverApi.Setup(a => a.ValidateAsync(It.IsAny<SaveGameServerRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new GameServerValidationResult { IsValid = true, Issues = [] });

        // Act
        var cut = Render<GameServerDetailsV2>(parameters => parameters.Add(p => p.ServerId, "srv-1"));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Valheim Viking World", cut.Markup);
            Assert.Contains("valheim-srv-1", cut.Markup);
            Assert.Contains("Running", cut.Markup);
            Assert.Contains("Ports", cut.Markup);
            Assert.Contains("Overview", cut.Markup);
        });
    }

    [Fact]
    public void GameServerDetailsV2_WhenServerIsAvailable_ShouldRenderAvailableBadge()
    {
        // Arrange
        var server = new GameServerDetail
        {
            ServerId = "srv-ready",
            Name = "Ready Minecraft Server",
            GameTypeDisplayName = "Minecraft",
            RevisionVersionTag = "1.21.2",
            ServiceName = "mc-srv-ready",
            Status = "Available"
        };

        serverApi.Setup(a => a.GetByServerIdAsync("srv-ready", It.IsAny<CancellationToken>())).ReturnsAsync(server);
        serverApi.Setup(a => a.ValidateAsync(It.IsAny<SaveGameServerRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new GameServerValidationResult { IsValid = true, Issues = [] });

        // Act
        var cut = Render<GameServerDetailsV2>(parameters => parameters.Add(p => p.ServerId, "srv-ready"));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Ready Minecraft Server", cut.Markup);
            Assert.Contains("Available", cut.Markup);
        });
    }

    [Fact]
    public void GameServerDetailsV2_WhenServerNotFound_ShouldRenderNotFoundMessage()
    {
        // Arrange
        serverApi.Setup(a => a.GetByServerIdAsync("missing-srv", It.IsAny<CancellationToken>())).ReturnsAsync((GameServerDetail?)null);

        // Act
        var cut = Render<GameServerDetailsV2>(parameters => parameters.Add(p => p.ServerId, "missing-srv"));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Server Not Found", cut.Markup);
        });
    }

    [Fact]
    public void GameServerDetailsV2_WhenAdvertisedPortsExist_ShouldDisplayBothHostNameAndPublicIpWithCopyButtons()
    {
        // Arrange
        var server = new GameServerDetail
        {
            ServerId = "srv-ports",
            Name = "Enshrouded World",
            GameTypeDisplayName = "Enshrouded",
            RevisionVersionTag = "1.0",
            ServiceName = "enshrouded-srv",
            Status = "Running",
            ResolvedPorts =
            [
                new GameServerResolvedPort { ContainerPort = 15636, PublishedPort = 15636, Protocol = "udp", Description = "Game Port", AdvertisedPort = true, DisplayOrder = 0 },
                new GameServerResolvedPort { ContainerPort = 15637, PublishedPort = 15637, Protocol = "udp", Description = "Query Port", AdvertisedPort = true, DisplayOrder = 1 }
            ]
        };

        serverApi.Setup(a => a.GetByServerIdAsync("srv-ports", It.IsAny<CancellationToken>())).ReturnsAsync(server);
        serverApi.Setup(a => a.ValidateAsync(It.IsAny<SaveGameServerRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameServerValidationResult { IsValid = true, Issues = [], ResolvedPorts = server.ResolvedPorts });

        // Act
        var cut = Render<GameServerDetailsV2>(parameters => parameters.Add(p => p.ServerId, "srv-ports"));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Connection Info", cut.Markup);
            Assert.Contains("HostName Address", cut.Markup);
            Assert.Contains("Public IP Address", cut.Markup);
            Assert.Contains("203.0.113.195:15636", cut.Markup);
            Assert.Contains("203.0.113.195:15637", cut.Markup);
            Assert.Contains("localhost:15636", cut.Markup);
            Assert.Contains("title=\"Copy HostName:Port\"", cut.Markup);
            Assert.Contains("title=\"Copy Public IP:Port\"", cut.Markup);
        });
    }

    [Fact]
    public void GameServerDetailsV2_WhenValidationIsValid_ShouldNotDisplayPinnedValidationBar()
    {
        // Arrange
        var server = new GameServerDetail
        {
            ServerId = "srv-valid",
            Name = "Perfect Server",
            GameTypeDisplayName = "Satisfactory",
            RevisionVersionTag = "1.0",
            ServiceName = "satisfactory-srv",
            Status = "Running"
        };

        serverApi.Setup(a => a.GetByServerIdAsync("srv-valid", It.IsAny<CancellationToken>())).ReturnsAsync(server);
        serverApi.Setup(a => a.ValidateAsync(It.IsAny<SaveGameServerRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameServerValidationResult { IsValid = true, Issues = [] });

        // Act
        var cut = Render<GameServerDetailsV2>(parameters => parameters.Add(p => p.ServerId, "srv-valid"));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Perfect Server", cut.Markup);
            Assert.Empty(cut.FindAll("div.gameserver-sticky-validation"));
            Assert.DoesNotContain("Current Validation:", cut.Markup);
        });
    }

    [Fact]
    public void GameServerDetailsV2_WhenValidationHasErrors_ShouldDisplayPinnedValidationBar()
    {
        // Arrange
        var server = new GameServerDetail
        {
            ServerId = "srv-invalid",
            Name = "Broken Server",
            GameTypeDisplayName = "Satisfactory",
            RevisionVersionTag = "1.0",
            ServiceName = "satisfactory-broken",
            Status = "Running"
        };

        serverApi.Setup(a => a.GetByServerIdAsync("srv-invalid", It.IsAny<CancellationToken>())).ReturnsAsync(server);
        serverApi.Setup(a => a.ValidateAsync(It.IsAny<SaveGameServerRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameServerValidationResult
            {
                IsValid = false,
                Issues = [new GameServerValidationIssue { Scope = "ports", Message = "Port conflict detected", Severity = "Error" }]
            });

        // Act
        var cut = Render<GameServerDetailsV2>(parameters => parameters.Add(p => p.ServerId, "srv-invalid"));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Broken Server", cut.Markup);
            Assert.NotEmpty(cut.FindAll("div.gameserver-sticky-validation"));
            Assert.Contains("Current Validation: 1 Issue", cut.Markup);
            Assert.Contains("Port conflict detected", cut.Markup);
        });
    }

    [Fact]
    public void GameServerDetailsV2_WhenPasswordSettingPresent_ShouldDisplayMaskedAndAllowToggle()
    {
        // Arrange
        var server = new GameServerDetail
        {
            ServerId = "srv-pwd",
            Name = "Protected Server",
            GameTypeKey = "valheim",
            GameTypeDisplayName = "Valheim",
            RevisionVersionTag = "1.0",
            ServiceName = "valheim-srv-pwd",
            Status = "Running",
            Settings =
            [
                new GameServerSetting { SettingKey = "SERVER_PASSWORD", Value = "SuperSecret123" },
                new GameServerSetting { SettingKey = "PUBLIC_NAME", Value = "My Server" }
            ]
        };

        var gameType = new GameTypeDetail
        {
            Key = "valheim",
            DisplayName = "Valheim",
            CurrentRevisionId = 1,
            Revisions =
            [
                new GameTypeRevision
                {
                    Id = 1,
                    VersionTag = "1.0",
                    SettingDefinitions =
                    [
                        new GameTypeSettingDefinition
                        {
                            SettingKey = "SERVER_PASSWORD",
                            Metadata = new GameTypeSettingMetadata { DataType = "password" }
                        },
                        new GameTypeSettingDefinition
                        {
                            SettingKey = "PUBLIC_NAME",
                            Metadata = new GameTypeSettingMetadata { DataType = "string" }
                        }
                    ]
                }
            ]
        };

        serverApi.Setup(a => a.GetByServerIdAsync("srv-pwd", It.IsAny<CancellationToken>())).ReturnsAsync(server);
        gameTypeApi.Setup(a => a.GetByKeyAsync("valheim", It.IsAny<CancellationToken>())).ReturnsAsync(gameType);
        serverApi.Setup(a => a.ValidateAsync(It.IsAny<SaveGameServerRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new GameServerValidationResult { IsValid = true, Issues = [] });

        // Act - Open to Settings tab (index 5)
        var cut = Render<GameServerDetailsV2>(parameters => parameters
            .Add(p => p.ServerId, "srv-pwd")
            .Add(p => p.SelectedTabIndex, 5));

        // Assert - Password should initially be masked
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("••••••••", cut.Markup);
            Assert.DoesNotContain("SuperSecret123", cut.Markup);
            Assert.Contains("My Server", cut.Markup);
        });

        // Find and click the toggle visibility button for password
        var showButton = cut.Find("button[title='Show Password']");
        showButton.Click();

        // Password should now be revealed
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("SuperSecret123", cut.Markup);
            Assert.NotNull(cut.Find("button[title='Hide Password']"));
        });
    }

    [Fact]
    public void GameServerDetailsV2_StartService_ShouldCallApiAndNotify()
    {
        var server = new GameServerDetail
        {
            ServerId = "srv-start",
            Name = "Stopped Minecraft",
            GameTypeDisplayName = "Minecraft",
            ServiceName = "mc-srv-start",
            Status = "Stopped"
        };
        var startedServer = new GameServerDetail
        {
            ServerId = "srv-start",
            Name = "Stopped Minecraft",
            GameTypeDisplayName = "Minecraft",
            ServiceName = "mc-srv-start",
            Status = "Running"
        };

        serverApi.Setup(a => a.GetByServerIdAsync("srv-start", It.IsAny<CancellationToken>())).ReturnsAsync(server);
        serverApi.Setup(a => a.StartAsync("srv-start", It.IsAny<CancellationToken>())).ReturnsAsync(startedServer);
        serverApi.Setup(a => a.ValidateAsync(It.IsAny<SaveGameServerRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new GameServerValidationResult { IsValid = true, Issues = [] });

        var cut = Render<GameServerDetailsV2>(parameters => parameters.Add(p => p.ServerId, "srv-start"));
        cut.WaitForAssertion(() => Assert.Contains("Stopped", cut.Markup));

        var startButton = cut.FindAll("button").First(b => b.TextContent.Contains("Start"));
        startButton.Click();

        cut.WaitForAssertion(() =>
        {
            serverApi.Verify(a => a.StartAsync("srv-start", It.IsAny<CancellationToken>()), Times.Once());
            Assert.Contains("Running", cut.Markup);
        });
    }

    [Fact]
    public void GameServerDetailsV2_StopService_ShouldCallApiAndNotify()
    {
        var server = new GameServerDetail
        {
            ServerId = "srv-stop",
            Name = "Running Minecraft",
            GameTypeDisplayName = "Minecraft",
            ServiceName = "mc-srv-stop",
            Status = "Running"
        };
        var stoppedServer = new GameServerDetail
        {
            ServerId = "srv-stop",
            Name = "Running Minecraft",
            GameTypeDisplayName = "Minecraft",
            ServiceName = "mc-srv-stop",
            Status = "Stopped"
        };

        serverApi.Setup(a => a.GetByServerIdAsync("srv-stop", It.IsAny<CancellationToken>())).ReturnsAsync(server);
        serverApi.Setup(a => a.StopAsync("srv-stop", It.IsAny<CancellationToken>())).ReturnsAsync(stoppedServer);
        serverApi.Setup(a => a.ValidateAsync(It.IsAny<SaveGameServerRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new GameServerValidationResult { IsValid = true, Issues = [] });

        var cut = Render<GameServerDetailsV2>(parameters => parameters.Add(p => p.ServerId, "srv-stop"));
        cut.WaitForAssertion(() => Assert.Contains("Running", cut.Markup));

        var stopButton = cut.FindAll("button").First(b => b.TextContent.Contains("Stop"));
        stopButton.Click();

        cut.WaitForAssertion(() =>
        {
            serverApi.Verify(a => a.StopAsync("srv-stop", It.IsAny<CancellationToken>()), Times.Once());
            Assert.Contains("Stopped", cut.Markup);
        });
    }

    [Fact]
    public void GameServerDetailsV2_RestartService_ShouldCallApi()
    {
        var server = new GameServerDetail
        {
            ServerId = "srv-restart",
            Name = "Running Server",
            GameTypeDisplayName = "Minecraft",
            ServiceName = "mc-srv-restart",
            Status = "Running"
        };

        serverApi.Setup(a => a.GetByServerIdAsync("srv-restart", It.IsAny<CancellationToken>())).ReturnsAsync(server);
        serverApi.Setup(a => a.RestartAsync("srv-restart", It.IsAny<CancellationToken>())).ReturnsAsync(server);
        serverApi.Setup(a => a.ValidateAsync(It.IsAny<SaveGameServerRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new GameServerValidationResult { IsValid = true, Issues = [] });

        var cut = Render<GameServerDetailsV2>(parameters => parameters.Add(p => p.ServerId, "srv-restart"));
        cut.WaitForAssertion(() => Assert.Contains("Running", cut.Markup));

        var restartButton = cut.FindAll("button").First(b => b.TextContent.Contains("Restart"));
        restartButton.Click();

        cut.WaitForAssertion(() =>
        {
            serverApi.Verify(a => a.RestartAsync("srv-restart", It.IsAny<CancellationToken>()), Times.Once());
        });
    }

    [Fact]
    public void GameServerDetailsV2_RedeployService_ShouldCallApi()
    {
        var server = new GameServerDetail
        {
            ServerId = "srv-redeploy",
            Name = "Server To Redeploy",
            GameTypeDisplayName = "Minecraft",
            ServiceName = "mc-srv-redeploy",
            Status = "Running"
        };

        serverApi.Setup(a => a.GetByServerIdAsync("srv-redeploy", It.IsAny<CancellationToken>())).ReturnsAsync(server);
        serverApi.Setup(a => a.RedeployAsync("srv-redeploy", It.IsAny<CancellationToken>())).ReturnsAsync(server);
        serverApi.Setup(a => a.ValidateAsync(It.IsAny<SaveGameServerRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new GameServerValidationResult { IsValid = true, Issues = [] });

        var cut = Render<GameServerDetailsV2>(parameters => parameters.Add(p => p.ServerId, "srv-redeploy"));
        cut.WaitForAssertion(() => Assert.Contains("Server To Redeploy", cut.Markup));

        var updateButton = cut.FindAll("button").First(b => b.TextContent.Contains("Update Service"));
        updateButton.Click();

        cut.WaitForAssertion(() =>
        {
            serverApi.Verify(a => a.RedeployAsync("srv-redeploy", It.IsAny<CancellationToken>()), Times.Once());
        });
    }

    [Fact]
    public void GameServerDetailsV2_DeleteServer_ShouldCallApiAndNavigate()
    {
        var server = new GameServerDetail
        {
            ServerId = "srv-del",
            Name = "Server To Delete",
            GameTypeDisplayName = "Minecraft",
            ServiceName = "mc-srv-del",
            Status = "Stopped"
        };

        serverApi.Setup(a => a.GetByServerIdAsync("srv-del", It.IsAny<CancellationToken>())).ReturnsAsync(server);
        serverApi.Setup(a => a.DeleteAsync("srv-del", true, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        serverApi.Setup(a => a.ValidateAsync(It.IsAny<SaveGameServerRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new GameServerValidationResult { IsValid = true, Issues = [] });

        var nav = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        var cut = Render<GameServerDetailsV2>(parameters => parameters.Add(p => p.ServerId, "srv-del"));
        cut.WaitForAssertion(() => Assert.Contains("Server To Delete", cut.Markup));

        var deleteButton = cut.FindAll("button").First(b => b.TextContent.Contains("Delete"));
        deleteButton.Click();

        cut.WaitForAssertion(() =>
        {
            serverApi.Verify(a => a.DeleteAsync("srv-del", true, It.IsAny<CancellationToken>()), Times.Once());
            Assert.EndsWith("/gameservers-v2", nav.Uri);
        });
    }

    [Theory]
    [InlineData("Preparing", "Preparing")]
    [InlineData("Starting", "Starting")]
    [InlineData("Stopped", "Stopped")]
    [InlineData("Stopping", "Stopping")]
    [InlineData("Failed", "Failed")]
    [InlineData("Deleted", "Deleted")]
    [InlineData("ScalingDown", "Scaling Down")]
    public void GameServerDetailsV2_DifferentStatuses_ShouldRenderExpectedStatusText(string status, string expectedText)
    {
        var server = new GameServerDetail
        {
            ServerId = $"srv-{status.ToLowerInvariant()}",
            Name = $"Server {status}",
            GameTypeDisplayName = "Minecraft",
            ServiceName = "mc-srv",
            Status = status
        };

        serverApi.Setup(a => a.GetByServerIdAsync(server.ServerId, It.IsAny<CancellationToken>())).ReturnsAsync(server);
        serverApi.Setup(a => a.ValidateAsync(It.IsAny<SaveGameServerRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new GameServerValidationResult { IsValid = true, Issues = [] });

        var cut = Render<GameServerDetailsV2>(parameters => parameters.Add(p => p.ServerId, server.ServerId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(expectedText, cut.Markup);
        });
    }
}
