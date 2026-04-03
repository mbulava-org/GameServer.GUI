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
    public void GameTypeRevisionPortsEditor_AddPort_ShouldCreateAdvertisedPrimaryPort()
    {
        // Arrange
        var ports = new List<GameTypeRevisionPortDraft>();

        // Act
        var cut = Render<GameTypeRevisionPortsEditor>(parameters => parameters
            .Add(p => p.Ports, ports)
            .Add(p => p.ProtocolOptions, new[] { "tcp", "udp" }));

        cut.FindAll("button").First(button => button.TextContent.Contains("Add Port", StringComparison.Ordinal)).Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Single(ports);
            Assert.True(ports[0].AdvertisedPort);
            Assert.Equal(25565, ports[0].ContainerPort);
        });
    }

    [Fact]
    public void GameTypeRevisionPortsEditor_AddSecondPort_ShouldKeepSingleAdvertisedPort()
    {
        // Arrange
        var ports = new List<GameTypeRevisionPortDraft>
        {
            new() { ContainerPort = 25565, Protocol = "tcp", AdvertisedPort = true, Description = "Primary" }
        };

        // Act
        var cut = Render<GameTypeRevisionPortsEditor>(parameters => parameters
            .Add(p => p.Ports, ports)
            .Add(p => p.ProtocolOptions, new[] { "tcp", "udp" }));

        cut.FindAll("button").First(button => button.TextContent.Contains("Add Port", StringComparison.Ordinal)).Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, ports.Count);
            Assert.Equal(1, ports.Count(port => port.AdvertisedPort));
            Assert.True(ports[0].AdvertisedPort);
            Assert.False(ports[1].AdvertisedPort);
        });
    }
}
