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

    [Fact]
    public async Task WebHostsEditor_BuildPathMoveRemoveAndPortVariable_ShouldWorkCorrectly()
    {
        // Arrange
        var h1 = new GameTypeRevisionWebHostDraft { Name = "Admin Panel", ContainerPort = 8080 };
        var h2 = new GameTypeRevisionWebHostDraft { Name = "Status Page", ContainerPortVariable = "STATUS_PORT" };
        var hosts = new List<GameTypeRevisionWebHostDraft> { h1, h2 };
        var changedCount = 0;

        var cut = Render<GameTypeRevisionWebHostsEditor>(parameters => parameters
            .Add(p => p.WebHosts, hosts)
            .Add(p => p.PortVariableOptions, [new WebHostPortVariableOption { SettingKey = "STATUS_PORT", Label = "STATUS_PORT (9090)", DefaultPort = 9090, IsCompatible = true }])
            .Add(p => p.OnDraftChanged, () => changedCount++));

        var instance = cut.Instance;
        var methodBuild = instance.GetType().GetMethod("BuildPathSegment", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodPortVar = instance.GetType().GetMethod("OnPortVariableChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodMove = instance.GetType().GetMethod("MoveWebHostDraftAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodRemove = instance.GetType().GetMethod("RemoveWebHostDraftAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Build path segment from name
        methodBuild!.Invoke(instance, [h1]);
        Assert.Equal("admin-panel", h1.PathSegment);

        // Change port variable on h1
        methodPortVar!.Invoke(instance, [h1, "STATUS_PORT"]);
        Assert.Equal("STATUS_PORT", h1.ContainerPortVariable);
        Assert.Null(h1.ContainerPort);

        // Move h1 down
        await (Task)methodMove!.Invoke(instance, [h1, 1])!;
        Assert.Equal(h2, hosts[0]);
        Assert.Equal(h1, hosts[1]);

        // Remove h2
        await (Task)methodRemove!.Invoke(instance, [h2])!;
        Assert.Single(hosts);
        Assert.Equal(h1, hosts[0]);

        Assert.True(changedCount > 0);
    }
}
