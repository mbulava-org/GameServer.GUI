using Bunit;
using GameServer.Web.Components.Layout;
using GameServer.Web.Components.Pages;
using Microsoft.Extensions.DependencyInjection;
using Radzen;

namespace GameServer.Web.Tests.Components.Layout;

public sealed class LayoutAndNavigationTests : BunitContext
{
    public LayoutAndNavigationTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<DialogService>();
        Services.AddSingleton<NotificationService>();
        Services.AddSingleton<TooltipService>();
        Services.AddSingleton<ContextMenuService>();
    }

    [Fact]
    public void NavMenu_ShouldRenderLinks()
    {
        // Act
        var cut = Render<NavMenu>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Home", cut.Markup);
            Assert.Contains("Servers", cut.Markup);
            Assert.Contains("Game Types", cut.Markup);
            Assert.Contains("Mount Types", cut.Markup);
        });
    }

    [Fact]
    public void MainLayout_ShouldRenderBodyAndProviders()
    {
        // Act
        var cut = Render<MainLayout>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("main"));
        });
    }

    [Fact]
    public void ReconnectModal_ShouldRenderComponents()
    {
        // Act
        var cut = Render<ReconnectModal>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("components-reconnect-modal", cut.Markup);
        });
    }

    [Fact]
    public void NotFound_ShouldRenderMessage()
    {
        // Act
        var cut = Render<NotFound>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Not Found", cut.Markup);
            Assert.Contains("Sorry, the content you are looking for does not exist.", cut.Markup);
        });
    }

    [Fact]
    public void Error_ShouldRenderErrorSummary()
    {
        // Act
        var cut = Render<Error>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("An error occurred while processing your request", cut.Markup);
        });
    }

    [Fact]
    public void Home_ShouldRenderHeroAndSections()
    {
        // Act
        var cut = Render<Home>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Game Server Manager", cut.Markup);
            Assert.Contains("Docker Swarm", cut.Markup);
            Assert.Contains("Create Your First Server", cut.Markup);
            Assert.Contains("View Servers", cut.Markup);
            Assert.Contains("Manage Game Types", cut.Markup);
        });
    }
}
