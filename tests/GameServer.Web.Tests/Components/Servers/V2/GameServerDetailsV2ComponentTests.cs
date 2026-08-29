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
}
