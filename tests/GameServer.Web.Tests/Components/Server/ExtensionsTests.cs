using Bunit;
using GameServer.Web.Components.Server.Extensions;
using GameServer.Web.Models.V2;
using GameServer.Web.Services.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Radzen;
using Radzen.Blazor;

namespace GameServer.Web.Tests.Components.Server;

public sealed class ExtensionsTests : BunitContext
{
    private readonly Mock<IRconClient> _rconClientMock = new();
    private readonly Mock<IPalworldApiClient> _palworldApiMock = new();

    public ExtensionsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<DialogService>();
        Services.AddSingleton<NotificationService>();
        Services.AddSingleton<TooltipService>();
        Services.AddSingleton(_rconClientMock.Object);
        Services.AddSingleton(_palworldApiMock.Object);
    }

    [Fact]
    public void NotYetImplementedTab_WhenRenderedWithoutDescriptor_ShowsDefaultWarning()
    {
        var cut = Render<NotYetImplementedTab>(p => p
            .Add(x => x.FailureReason, "Component type missing"));

        Assert.Contains("Extension not available", cut.Markup);
        Assert.Contains("Component type missing", cut.Markup);
    }

    [Fact]
    public void NotYetImplementedTab_WhenDescriptorProvided_RendersTitleAndCopyButton()
    {
        var descriptor = new GameTypeUiExtensionDescriptor
        {
            Title = "Custom Dashboard",
            ComponentTypeName = "Custom.DashboardComponent",
            AssemblyName = "Custom.Assembly"
        };

        var cut = Render<NotYetImplementedTab>(p => p
            .Add(x => x.Descriptor, descriptor));

        Assert.Contains("Custom Dashboard", cut.Markup);
        Assert.Contains("Custom.DashboardComponent", cut.Markup);
        Assert.Contains("Custom.Assembly", cut.Markup);

        var copyBtn = cut.Find("button");
        Assert.NotNull(copyBtn);
        copyBtn.Click();
    }

    [Fact]
    public void RconTab_WhenServerNull_ShowsNoServerAlert()
    {
        var cut = Render<RconTab>(p => p.Add(x => x.Server, null));
        Assert.Contains("No server context provided to the RCON extension", cut.Markup);
    }

    [Fact]
    public void RconTab_WhenRconDisabled_ShowsDisabledWarning()
    {
        var server = new GameServerDetail
        {
            ServerId = "srv-rcon",
            Settings =
            [
                new GameServerSetting { SettingKey = "RCON_ENABLED", Value = "False" }
            ]
        };

        var cut = Render<RconTab>(p => p.Add(x => x.Server, server));
        Assert.Contains("RCON is disabled for this server", cut.Markup);
    }

    [Fact]
    public void RconTab_WhenRconEnabledButPortMissing_ShowsMissingPortWarning()
    {
        var server = new GameServerDetail
        {
            ServerId = "srv-rcon",
            Settings =
            [
                new GameServerSetting { SettingKey = "RCON_ENABLED", Value = "True" },
                new GameServerSetting { SettingKey = "ADMIN_PASSWORD", Value = "secret" }
            ]
        };

        var cut = Render<RconTab>(p => p.Add(x => x.Server, server));
        Assert.Contains("RCON is enabled but", cut.Markup);
    }

    [Fact]
    public async Task RconTab_WhenRconFullyConfigured_CanRunCommandsAndClearLog()
    {
        var server = new GameServerDetail
        {
            ServerId = "srv-rcon",
            ServiceName = "service-rcon",
            Settings =
            [
                new GameServerSetting { SettingKey = "RCON_ENABLED", Value = "True" },
                new GameServerSetting { SettingKey = "RCON_PORT", Value = "25575" },
                new GameServerSetting { SettingKey = "ADMIN_PASSWORD", Value = "secret123" }
            ]
        };

        var extParams = new Dictionary<string, string>
        {
            ["presetCommands"] = "help,status,save"
        };

        _rconClientMock.Setup(c => c.ExecuteAsync(It.IsAny<RconRequestContext>(), "status", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RconCommandResult { Success = true, Response = "Server online, 5 players" });

        _rconClientMock.Setup(c => c.ExecuteAsync(It.IsAny<RconRequestContext>(), "save", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RconCommandResult { Success = false, Error = "Save failed" });

        var cut = Render<RconTab>(p => p
            .Add(x => x.Server, server)
            .Add(x => x.ExtensionParameters, extParams));

        Assert.Contains("RCON Console", cut.Markup);

        // Click preset button 'status'
        var statusBtn = cut.FindComponents<RadzenButton>().FirstOrDefault(b => b.Instance.Text == "status");
        Assert.NotNull(statusBtn);
        await cut.InvokeAsync(() => statusBtn.Find("button").Click());

        Assert.Contains("Server online, 5 players", cut.Markup);

        // Click preset button 'save' (failure case)
        var saveBtn = cut.FindComponents<RadzenButton>().FirstOrDefault(b => b.Instance.Text == "save");
        Assert.NotNull(saveBtn);
        await cut.InvokeAsync(() => saveBtn.Find("button").Click());

        Assert.Contains("Save failed", cut.Markup);

        // Click Clear button
        var clearBtn = cut.FindComponents<RadzenButton>().FirstOrDefault(b => b.Instance.Text == "Clear");
        Assert.NotNull(clearBtn);
        await cut.InvokeAsync(() => clearBtn.Find("button").Click());

        Assert.Contains("No commands executed yet", cut.Markup);
    }

    [Fact]
    public void PalworldApiTab_WhenServerNull_ShowsNoServerAlert()
    {
        var cut = Render<PalworldApiTab>(p => p.Add(x => x.Server, null));
        Assert.Contains("No server context provided to the Palworld extension", cut.Markup);
    }

    [Fact]
    public void PalworldApiTab_WhenRestApiDisabled_ShowsDisabledWarning()
    {
        var server = new GameServerDetail
        {
            ServerId = "srv-pal",
            Settings =
            [
                new GameServerSetting { SettingKey = "REST_API_ENABLED", Value = "False" }
            ]
        };

        var cut = Render<PalworldApiTab>(p => p.Add(x => x.Server, server));
        Assert.Contains("The Palworld REST API is disabled for this server", cut.Markup);
    }

    [Fact]
    public async Task PalworldApiTab_WhenRestApiEnabledAndConfigured_CanPerformActions()
    {
        var server = new GameServerDetail
        {
            ServerId = "srv-pal",
            ServiceName = "service-pal",
            Settings =
            [
                new GameServerSetting { SettingKey = "REST_API_ENABLED", Value = "True" },
                new GameServerSetting { SettingKey = "REST_API_PORT", Value = "8212" },
                new GameServerSetting { SettingKey = "ADMIN_PASSWORD", Value = "palpass" }
            ]
        };

        _palworldApiMock.Setup(a => a.GetInfoAsync(It.IsAny<PalworldRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PalworldServerInfo { ServerName = "Palworld Dedicated", Version = "v0.3.5", WorldGuid = "world-guid-123", Description = "Great server" });

        _palworldApiMock.Setup(a => a.GetPlayersAsync(It.IsAny<PalworldRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PalworldPlayersResponse
            {
                Players = [new PalworldPlayer { Name = "Gamer1", Level = 25, Ping = 12.5, UserId = "steam_12345" }]
            });

        _palworldApiMock.Setup(a => a.SaveWorldAsync(It.IsAny<PalworldRequestContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _palworldApiMock.Setup(a => a.ShutdownAsync(It.IsAny<PalworldRequestContext>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _palworldApiMock.Setup(a => a.KickPlayerAsync(It.IsAny<PalworldRequestContext>(), "steam_12345", null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _palworldApiMock.Setup(a => a.BanPlayerAsync(It.IsAny<PalworldRequestContext>(), "steam_12345", null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cut = Render<PalworldApiTab>(p => p.Add(x => x.Server, server));

        Assert.Contains("Server Info", cut.Markup);

        // Click Info Refresh
        var refreshBtns = cut.FindComponents<RadzenButton>().Where(b => b.Instance.Text == "Refresh").ToList();
        Assert.NotEmpty(refreshBtns);
        await cut.InvokeAsync(() => refreshBtns[0].Find("button").Click());
        Assert.Contains("Palworld Dedicated", cut.Markup);

        // Click Players Refresh
        if (refreshBtns.Count > 1)
        {
            await cut.InvokeAsync(() => refreshBtns[1].Find("button").Click());
            Assert.Contains("Gamer1", cut.Markup);

            // Click Kick button
            var kickBtn = cut.FindComponents<RadzenButton>().FirstOrDefault(b => b.Instance.Text == "Kick");
            if (kickBtn != null)
            {
                await cut.InvokeAsync(() => kickBtn.Find("button").Click());
                _palworldApiMock.Verify(a => a.KickPlayerAsync(It.IsAny<PalworldRequestContext>(), "steam_12345", null, It.IsAny<CancellationToken>()), Times.Once);
            }

            // Click Ban button
            var banBtn = cut.FindComponents<RadzenButton>().FirstOrDefault(b => b.Instance.Text == "Ban");
            if (banBtn != null)
            {
                await cut.InvokeAsync(() => banBtn.Find("button").Click());
                _palworldApiMock.Verify(a => a.BanPlayerAsync(It.IsAny<PalworldRequestContext>(), "steam_12345", null, It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        // Click Save World
        var saveBtn = cut.FindComponents<RadzenButton>().FirstOrDefault(b => b.Instance.Text == "Save World");
        Assert.NotNull(saveBtn);
        await cut.InvokeAsync(() => saveBtn.Find("button").Click());
        _palworldApiMock.Verify(a => a.SaveWorldAsync(It.IsAny<PalworldRequestContext>(), It.IsAny<CancellationToken>()), Times.Once);

        // Click Shutdown
        var shutdownBtn = cut.FindComponents<RadzenButton>().FirstOrDefault(b => b.Instance.Text == "Shutdown");
        Assert.NotNull(shutdownBtn);
        await cut.InvokeAsync(() => shutdownBtn.Find("button").Click());
        _palworldApiMock.Verify(a => a.ShutdownAsync(It.IsAny<PalworldRequestContext>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
