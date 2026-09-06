using Bunit;
using GameServer.Web.Components.Pages;
using GameServer.Web.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Radzen;

namespace GameServer.Web.Tests.Components.Pages;

public class SignalRTestPageTests : BunitContext
{
    public SignalRTestPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddScoped<DialogService>();
        Services.AddScoped<NotificationService>();
        Services.AddScoped<TooltipService>();
        Services.AddScoped<ContextMenuService>();
        Services.AddSingleton<IOptions<GameServerDockerApi>>(Options.Create(new GameServerDockerApi
        {
            BaseUri = "http://localhost:5164/"
        }));
    }

    [Fact]
    public void SignalRTestPage_RendersSuccessfully()
    {
        var cut = Render<SignalRTest>();
        Assert.NotNull(cut);
        Assert.Contains("SignalR Resource Monitor - Connection Test", cut.Markup);
        Assert.Contains("Connection Status", cut.Markup);
        Assert.Contains("Subscription Controls", cut.Markup);
        Assert.Contains("SignalR Message Log", cut.Markup);
    }

    [Fact]
    public void SignalRTestPage_ContainsConnectAndClearButtons()
    {
        var cut = Render<SignalRTest>();
        var buttons = cut.FindAll("button");
        Assert.Contains(buttons, b => b.TextContent.Contains("Connect"));
        Assert.Contains(buttons, b => b.TextContent.Contains("Clear Logs"));
    }

    [Fact]
    public void SignalRTestPage_ClickClearLogs_ClearsLogEntries()
    {
        var cut = Render<SignalRTest>();
        var clearBtn = cut.FindAll("button").First(b => b.TextContent.Contains("Clear Logs"));
        clearBtn.Click();

        Assert.Contains("Logs cleared", cut.Markup);
    }
}
