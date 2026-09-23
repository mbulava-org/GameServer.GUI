using System.Collections.Generic;
using Bunit;
using GameServer.Web.Components.Server.Extensions;
using GameServer.Web.Models.V2;
using GameServer.Web.Services.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Radzen;
using Xunit;

namespace GameServer.Web.Tests.Components.Server.Extensions;

public sealed class RconTabTests : BunitContext
{
    private readonly Mock<IRconClient> _rconClientMock;

    public RconTabTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _rconClientMock = new Mock<IRconClient>();

        Services.AddSingleton<IRconClient>(_rconClientMock.Object);
        Services.AddSingleton<NotificationService>();
    }

    [Fact]
    public void RconTab_WhenServerIsNull_ShouldRenderNoServerAlert()
    {
        var cut = Render<RconTab>(p => p
            .Add(c => c.Server, null));

        Assert.Contains("No server context provided to the RCON extension", cut.Markup);
    }

    [Fact]
    public void RconTab_WhenNoParametersConfigured_ShouldRenderNotConfiguredWarning()
    {
        var server = new GameServerDetail
        {
            ServiceName = "test-service",
            Settings = []
        };

        var cut = Render<RconTab>(p => p
            .Add(c => c.Server, server));

        // When parameters are not supplied, port and password are empty / unconfigured
        Assert.Contains("RCON cannot connect", cut.Markup);
        Assert.Contains("port is not configured", cut.Markup);
        Assert.Contains("password is not configured", cut.Markup);
    }

    [Fact]
    public void RconTab_WhenRconDisabledViaConfiguredSetting_ShouldRenderDisabledWarning()
    {
        var server = new GameServerDetail
        {
            ServiceName = "test-service",
            Settings =
            [
                new GameServerSetting { SettingKey = "RCON_ENABLED", Value = "false" }
            ]
        };

        var extParams = new Dictionary<string, string>
        {
            { "enabledSettingKey", "RCON_ENABLED" }
        };

        var cut = Render<RconTab>(p => p
            .Add(c => c.Server, server)
            .Add(c => c.ExtensionParameters, extParams));

        Assert.Contains("RCON is disabled for this server", cut.Markup);
        Assert.Contains("RCON_ENABLED=True", cut.Markup);
    }

    [Fact]
    public void RconTab_WhenNoEnableSettingSupplied_ShouldNotGateOnEnable()
    {
        var server = new GameServerDetail
        {
            ServiceName = "test-service",
            Settings = []
        };

        var extParams = new Dictionary<string, string>
        {
            { "port", "25575" },
            { "password", "secret" }
        };

        var cut = Render<RconTab>(p => p
            .Add(c => c.Server, server)
            .Add(c => c.ExtensionParameters, extParams));

        // Since no enableSettingKey was supplied, it is not used, so RCON connects immediately
        Assert.Contains("RCON Console", cut.Markup);
        Assert.Contains("Enter RCON command", cut.Markup);
    }

    [Fact]
    public void RconTab_WhenPortAndPassConfiguredInSettings_ShouldRenderConsole()
    {
        var server = new GameServerDetail
        {
            ServiceName = "test-service",
            Settings =
            [
                new GameServerSetting { SettingKey = "RCON_PORT", Value = "25575" },
                new GameServerSetting { SettingKey = "RCON_PASSWORD", Value = "secret" }
            ]
        };

        var extParams = new Dictionary<string, string>
        {
            { "portSettingKey", "RCON_PORT" },
            { "passwordSettingKey", "RCON_PASSWORD" }
        };

        var cut = Render<RconTab>(p => p
            .Add(c => c.Server, server)
            .Add(c => c.ExtensionParameters, extParams));

        Assert.Contains("RCON Console", cut.Markup);
        Assert.Contains("Enter RCON command", cut.Markup);
    }

    [Fact]
    public void RconTab_WhenHardcodedPortAndPasswordInExtensionParameters_ShouldRenderConsole()
    {
        var server = new GameServerDetail
        {
            ServiceName = "test-service",
            Settings = []
        };

        var extParams = new Dictionary<string, string>
        {
            { "port", "25575" },
            { "password", "secret" },
            { "presetCommands", "Info, Save" }
        };

        var cut = Render<RconTab>(p => p
            .Add(c => c.Server, server)
            .Add(c => c.ExtensionParameters, extParams));

        Assert.Contains("RCON Console", cut.Markup);
        Assert.Contains("Enter RCON command", cut.Markup);
        Assert.Contains("Info", cut.Markup);
        Assert.Contains("Save", cut.Markup);
    }
}
