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
}
