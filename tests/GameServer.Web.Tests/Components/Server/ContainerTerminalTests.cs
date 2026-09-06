using Bunit;
using GameServer.Web.Components.Server;
using GameServer.Web.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Radzen;

namespace GameServer.Web.Tests.Components.Server;

[Collection("XtermTests")]
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

    [Fact]
    public async Task ContainerTerminal_InternalEventHandlers_ShouldExecuteSafely()
    {
        // Arrange
        var cut = Render<ContainerTerminal>(parameters => parameters
            .Add(p => p.ServerId, "srv-1")
            .Add(p => p.ContainerId, "cnt-1")
            .Add(p => p.AutoConnect, false));

        var instance = cut.Instance;
        var methodClear = instance.GetType().GetMethod("ClearTerminal", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodDisconnect = instance.GetType().GetMethod("DisconnectAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodOutput = instance.GetType().GetMethod("OnTerminalOutput", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodError = instance.GetType().GetMethod("OnTerminalError", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodStarted = instance.GetType().GetMethod("OnTerminalSessionStarted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodDisconnected = instance.GetType().GetMethod("OnTerminalDisconnected", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodData = instance.GetType().GetMethod("OnTerminalData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act & Assert
        await cut.InvokeAsync(async () =>
        {
            methodStarted!.Invoke(instance, [null, "sess-1"]);
            methodOutput!.Invoke(instance, [null, "output line\n"]);
            methodError!.Invoke(instance, [null, "error line\n"]);
            await (Task)methodData!.Invoke(instance, ["ls\r"])!;
            await (Task)methodClear!.Invoke(instance, [])!;
            methodDisconnected!.Invoke(instance, [null, "closed"]);
            await (Task)methodDisconnect!.Invoke(instance, [])!;
        });
    }
}
