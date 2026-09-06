using Bunit;
using GameServer.Web.Components.Server;
using GameServer.Web.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Radzen;

namespace GameServer.Web.Tests.Components.Server;

[Collection("XtermTests")]
public sealed class ContainerConsoleTests : BunitContext
{
    public ContainerConsoleTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<DialogService>();
        Services.AddSingleton<NotificationService>();
        Services.AddSingleton<TooltipService>();
        Services.AddSingleton<IOptions<GameServerDockerApi>>(Options.Create(new GameServerDockerApi
        {
            BaseUri = "http://localhost:5164/"
        }));
    }

    [Fact]
    public void ContainerConsole_WhenRenderedInitially_ShouldShowDisconnectedStateWithConnectButton()
    {
        // Act
        var cut = Render<ContainerConsole>(parameters => parameters
            .Add(p => p.ServerId, "srv-1")
            .Add(p => p.AutoConnect, false));

        // Assert
        Assert.Contains("srv-1", cut.Markup);
        Assert.Contains("Disconnected", cut.Markup);
        Assert.Contains("Connect", cut.Markup);
    }

    [Fact]
    public void ContainerConsole_WhenAutoConnectEnabled_ShouldAttemptConnectionWithoutCrashing()
    {
        // Act
        var cut = Render<ContainerConsole>(parameters => parameters
            .Add(p => p.ServerId, "srv-1")
            .Add(p => p.AutoConnect, true));

        // Assert - Component renders safely
        Assert.NotNull(cut.Markup);
    }

    [Fact]
    public async Task ContainerConsole_InternalEventHandlers_ShouldExecuteSafely()
    {
        // Arrange
        var cut = Render<ContainerConsole>(parameters => parameters
            .Add(p => p.ServerId, "srv-1")
            .Add(p => p.AutoConnect, false));

        var instance = cut.Instance;
        var methodClear = instance.GetType().GetMethod("ClearTerminal", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodDisconnect = instance.GetType().GetMethod("DisconnectAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodOutput = instance.GetType().GetMethod("OnOutputReceived", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodError = instance.GetType().GetMethod("OnErrorReceived", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodConnected = instance.GetType().GetMethod("OnConsoleConnected", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodDisconnected = instance.GetType().GetMethod("OnConsoleDisconnected", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodControl = instance.GetType().GetMethod("OnInputControlChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodData = instance.GetType().GetMethod("OnTerminalData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodLineFeed = instance.GetType().GetMethod("OnLineFeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodFocus = instance.GetType().GetMethod("FocusTerminalAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act & Assert each handler executes safely without throwing unhandled exceptions
        await cut.InvokeAsync(async () =>
        {
            methodConnected!.Invoke(instance, [null, "conn-123"]);
            methodOutput!.Invoke(instance, [null, "stdout data"]);
            methodError!.Invoke(instance, [null, "stderr error"]);
            methodControl!.Invoke(instance, [null, "user-1"]);
            methodControl.Invoke(instance, [null, ""]);
            await (Task)methodData!.Invoke(instance, ["test data\r"])!;
            await (Task)methodLineFeed!.Invoke(instance, [])!;
            await (Task)methodFocus!.Invoke(instance, [])!;
            await (Task)methodClear!.Invoke(instance, [])!;
            methodDisconnected!.Invoke(instance, [null, "connection closed"]);
            await (Task)methodDisconnect!.Invoke(instance, [])!;
        });
    }
}
