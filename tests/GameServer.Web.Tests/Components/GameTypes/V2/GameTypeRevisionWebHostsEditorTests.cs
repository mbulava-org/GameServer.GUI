using Bunit;
using GameServer.Web.Components.Pages.GameTypes.Components.V2;

namespace GameServer.Web.Tests.Components.GameTypes.V2;

public sealed class GameTypeRevisionWebHostsEditorTests : BunitContext
{
    public GameTypeRevisionWebHostsEditorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void WebHostsEditor_WhenEmpty_ShouldRenderEmptyMessage()
    {
        // Arrange & Act
        var cut = Render<GameTypeRevisionWebHostsEditor>(parameters => parameters
            .Add(p => p.WebHosts, []));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("No Web Host rules in this draft.", cut.Markup);
            Assert.Contains("Add Web Host", cut.Markup);
        });
    }

    [Fact]
    public void WebHostsEditor_WithHosts_ShouldRenderFields()
    {
        // Arrange
        var hosts = new List<GameTypeRevisionWebHostDraft>
        {
            new()
            {
                Name = "Live Map",
                PathSegment = "map",
                ContainerPort = 8123,
                Description = "Dynmap Web UI",
                EnabledWhen = "ENABLE_MAP"
            }
        };

        // Act
        var cut = Render<GameTypeRevisionWebHostsEditor>(parameters => parameters
            .Add(p => p.WebHosts, hosts)
            .Add(p => p.PortVariableOptions, [new WebHostPortVariableOption { SettingKey = "MAP_PORT", Label = "MAP_PORT (8123)", DefaultPort = 8123 }]));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Live Map", cut.Markup);
            Assert.Contains("Path Segment", cut.Markup);
            Assert.Contains("Static Port", cut.Markup);
            Assert.Contains("Port Variable", cut.Markup);
        });
    }

    [Fact]
    public void WebHostsEditor_AddWebHost_ShouldAddDraft()
    {
        // Arrange
        var hosts = new List<GameTypeRevisionWebHostDraft>();
        var changed = false;

        var cut = Render<GameTypeRevisionWebHostsEditor>(parameters => parameters
            .Add(p => p.WebHosts, hosts)
            .Add(p => p.OnDraftChanged, () => changed = true));

        // Act
        var addButton = cut.FindAll("button").First(b => b.TextContent.Contains("Add Web Host"));
        addButton.Click();

        // Assert
        Assert.Single(hosts);
        Assert.True(changed);
    }
}
