using Bunit;
using GameServer.Web.Components.Pages.Servers;
using GameServer.Web.Models.V2;
using GameServer.Web.Services;
using GameServer.Web.Services.V2;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Radzen;

namespace GameServer.Web.Tests.Components.Servers.V2;

public sealed class GameServerManagerV2ComponentTests : BunitContext
{
    private readonly Mock<IGameServerV2ApiService> api = new();
    private readonly Mock<IThumbnailCacheService> thumbnailCache = new();

    public GameServerManagerV2ComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<DialogService>();
        Services.AddSingleton<NotificationService>();
        Services.AddSingleton<TooltipService>();
        Services.AddSingleton(api.Object);
        Services.AddSingleton(thumbnailCache.Object);
        thumbnailCache
            .Setup(t => t.GetCachedThumbnailUrlAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string? url, CancellationToken _) => url);
    }

    [Fact]
    public void GameServerManagerV2_WhenServersExist_ShouldRenderGridWithDetails()
    {
        // Arrange
        var servers = new List<GameServerListItem>
        {
            new()
            {
                ServerId = "srv-1",
                Name = "Survival World",
                GameTypeDisplayName = "Minecraft",
                RevisionVersionTag = "1.21.2",
                Status = "Running",
                ResolvedPorts =
                [
                    new GameServerResolvedPort { ContainerPort = 25565, Protocol = "tcp" }
                ]
            },
            new()
            {
                ServerId = "srv-2",
                Name = "Creative Plot",
                GameTypeDisplayName = "Minecraft",
                RevisionVersionTag = "1.21.2",
                Status = "Stopped",
                ResolvedPorts = []
            }
        };

        api.Setup(a => a.GetListAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(servers);

        // Act
        var cut = Render<GameServerManagerV2>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("New Server", cut.Markup);
            Assert.Contains("Survival World", cut.Markup);
            Assert.Contains("Creative Plot", cut.Markup);
            Assert.Contains("Running", cut.Markup);
            Assert.Contains("Stopped", cut.Markup);
            Assert.Contains("25565/tcp", cut.Markup);
        });
    }

    [Fact]
    public void GameServerManagerV2_WhenEmpty_ShouldRenderEmptyMessage()
    {
        // Arrange
        api.Setup(a => a.GetListAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<GameServerListItem>());

        // Act
        var cut = Render<GameServerManagerV2>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("New Server", cut.Markup);
            Assert.Contains("Game Servers", cut.Markup);
        });
    }

    [Fact]
    public void GameServerManagerV2_WhenDeleteClicked_ShouldCallApiAndDelete()
    {
        // Arrange
        var servers = new List<GameServerListItem>
        {
            new()
            {
                ServerId = "srv-delete",
                Name = "Server To Delete",
                Status = "Stopped"
            }
        };

        api.Setup(a => a.GetListAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(servers);
        api.Setup(a => a.DeleteAsync("srv-delete", It.IsAny<bool>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var cut = Render<GameServerManagerV2>();
        cut.WaitForAssertion(() => Assert.Contains("Server To Delete", cut.Markup));

        // Act
        var deleteButton = cut.FindAll("button").First(b => b.ClassList.Contains("rz-danger"));
        deleteButton.Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            api.Verify(a => a.DeleteAsync("srv-delete", It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        });
    }
}
