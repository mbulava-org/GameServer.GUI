using Bunit;
using GameServer.Web.Components.Pages.GameTypes.Components.V2;

namespace GameServer.Web.Tests.Components.GameTypes.V2;

public sealed class GameTypeRevisionPortsEditorTests : BunitContext
{
    public GameTypeRevisionPortsEditorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void PortsEditor_WhenEmpty_ShouldRenderEmptyMessage()
    {
        // Act
        var cut = Render<GameTypeRevisionPortsEditor>(parameters => parameters
            .Add(p => p.Ports, [])
            .Add(p => p.ProtocolOptions, ["tcp", "udp"]));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("No ports in this draft.", cut.Markup);
            Assert.Contains("Add Port", cut.Markup);
        });
    }

    [Fact]
    public void PortsEditor_WithPorts_ShouldRenderFields()
    {
        // Arrange
        var ports = new List<GameTypeRevisionPortDraft>
        {
            new() { ContainerPort = 25565, Protocol = "tcp", AdvertisedPort = true, Description = "Default Minecraft Port" }
        };

        // Act
        var cut = Render<GameTypeRevisionPortsEditor>(parameters => parameters
            .Add(p => p.Ports, ports)
            .Add(p => p.ProtocolOptions, ["tcp", "udp"]));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Container Port", cut.Markup);
            Assert.Contains("25565", cut.Markup);
            Assert.Contains("Advertised", cut.Markup);
            Assert.Contains("Default Minecraft Port", cut.Markup);
        });
    }

    [Fact]
    public void PortsEditor_AddPort_ShouldAddPortDraft()
    {
        // Arrange
        var ports = new List<GameTypeRevisionPortDraft>();
        var changed = false;

        var cut = Render<GameTypeRevisionPortsEditor>(parameters => parameters
            .Add(p => p.Ports, ports)
            .Add(p => p.ProtocolOptions, ["tcp", "udp"])
            .Add(p => p.OnDraftChanged, () => changed = true));

        // Act
        var addButton = cut.FindAll("button").First(b => b.TextContent.Contains("Add Port"));
        addButton.Click();

        // Assert
        Assert.Single(ports);
        Assert.True(ports[0].AdvertisedPort);
        Assert.True(changed);
    }

    [Fact]
    public async Task PortsEditor_RemoveMoveAndSetAdvertised_ShouldUpdateListCorrectly()
    {
        // Arrange
        var p1 = new GameTypeRevisionPortDraft { ContainerPort = 8080, Protocol = "tcp", AdvertisedPort = true, Description = "Web" };
        var p2 = new GameTypeRevisionPortDraft { ContainerPort = 9090, Protocol = "udp", AdvertisedPort = false, Description = "Query" };
        var ports = new List<GameTypeRevisionPortDraft> { p1, p2 };
        var changedCount = 0;

        var cut = Render<GameTypeRevisionPortsEditor>(parameters => parameters
            .Add(p => p.Ports, ports)
            .Add(p => p.ProtocolOptions, ["tcp", "udp"])
            .Add(p => p.OnDraftChanged, () => changedCount++));

        var instance = cut.Instance;
        var methodMove = instance.GetType().GetMethod("MovePortDraftAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodSetAdv = instance.GetType().GetMethod("SetAdvertisedPortAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodRemove = instance.GetType().GetMethod("RemovePortDraftAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Move p1 down
        await (Task)methodMove!.Invoke(instance, [p1, 1])!;
        Assert.Equal(p2, ports[0]);
        Assert.Equal(p1, ports[1]);

        // Move out of bounds (should be no-op)
        await (Task)methodMove.Invoke(instance, [p1, 10])!;
        Assert.Equal(p1, ports[1]);

        // Set p2 as advertised
        await (Task)methodSetAdv!.Invoke(instance, [p2, true])!;
        Assert.True(p2.AdvertisedPort);
        Assert.False(p1.AdvertisedPort);

        // Unsetting advertised when no other advertised defaults back to true
        await (Task)methodSetAdv.Invoke(instance, [p2, false])!;
        Assert.True(p2.AdvertisedPort);

        // Remove p2 (p1 should now become advertised because no other port is advertised)
        await (Task)methodRemove!.Invoke(instance, [p2])!;
        Assert.Single(ports);
        Assert.Equal(p1, ports[0]);
        Assert.True(p1.AdvertisedPort);

        Assert.True(changedCount > 0);
    }
}
