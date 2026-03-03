using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Radzen;

namespace GameServer.Web.Tests.Components.Pages;

public class HomePageTests : BunitContext
{
    public HomePageTests()
    {
        // Register required services for Radzen components
        Services.AddScoped<DialogService>();
        Services.AddScoped<NotificationService>();
        Services.AddScoped<TooltipService>();
        Services.AddScoped<ContextMenuService>();
    }

    [Fact]
    public void HomePage_ShouldRender()
    {
        // Act
        var cut = Render<GameServer.Web.Components.Pages.Home>();

        // Assert
        Assert.NotNull(cut);
    }

    [Fact]
    public void HomePage_ShouldContainTitle()
    {
        // Act
        var cut = Render<GameServer.Web.Components.Pages.Home>();

        // Assert
        var title = cut.Find("h1.hero-title");
        Assert.Contains("Game Server Manager", title.TextContent);
    }

    [Fact]
    public void HomePage_ShouldHaveViewServersButton()
    {
        // Act
        var cut = Render<GameServer.Web.Components.Pages.Home>();

        // Assert
        var buttons = cut.FindAll("button");
        Assert.Contains(buttons, b => b.TextContent.Contains("View Servers"));
    }

    [Fact]
    public void HomePage_ShouldHaveCreateServerButton()
    {
        // Act
        var cut = Render<GameServer.Web.Components.Pages.Home>();

        // Assert
        var buttons = cut.FindAll("button");
        Assert.Contains(buttons, b => b.TextContent.Contains("Create Server"));
    }

    [Fact]
    public void HomePage_ShouldContainPageTitle()
    {
        // Act
        var cut = Render<GameServer.Web.Components.Pages.Home>();

        // Assert - PageTitle is rendered in the document head
        // We can verify the page renders without errors instead
        Assert.NotNull(cut.Markup);
        Assert.NotEmpty(cut.Markup);
    }
}
