using Bunit;
using GameServer.Web.Components.Server;
using GameServer.Web.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Radzen;

namespace GameServer.Web.Tests.Components.Server;

public sealed class ContainerTerminalTests : BunitContext
{
    public ContainerTerminalTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<DialogService>();
        Services.AddSingleton<NotificationService>();
        Services.AddSingleton<TooltipService>();
        Services.AddSingleton<ILogger<ContainerTerminal>>(NullLogger<ContainerTerminal>.Instance);
        Services.AddSingleton<IOptions<GameServerDockerApi>>(Options.Create(new GameServerDockerApi
        {
            BaseUri = "http://localhost:5164/"
        }));
    }

    [Fact]
    public void ContainerTerminal_WhenRenderedInitially_ShouldShowDisconnectedStateWithConnectButton()
    {
        // Act
        var cut = Render<ContainerTerminal>(parameters => parameters
            .Add(p => p.ServerId, "srv-1")
            .Add(p => p.ContainerId, "cnt-1")
            .Add(p => p.AutoConnect, false));

        // Assert
        Assert.Contains("Interactive Shell", cut.Markup);
        Assert.Contains("Disconnected", cut.Markup);
        Assert.Contains("Connect", cut.Markup);
    }

    [Fact]
    public void ContainerTerminal_WhenAutoConnectEnabled_ShouldAttemptConnectionWithoutCrashing()
    {
        // Act
        var cut = Render<ContainerTerminal>(parameters => parameters
            .Add(p => p.ServerId, "srv-1")
            .Add(p => p.ContainerId, "cnt-1")
            .Add(p => p.AutoConnect, true));

        // Assert - Component renders safely
        Assert.NotNull(cut.Markup);
    }
}
