using Bunit;
using GameServer.Web.Components.Pages.GameTypes.Components.V2;
using Microsoft.Extensions.DependencyInjection;
using Radzen;
using Radzen.Blazor;
using Xunit;

namespace GameServer.Web.Tests.Components.GameTypes.V2;

public sealed class GameTypeRevisionHealthcheckEditorTests : BunitContext
{
    public GameTypeRevisionHealthcheckEditorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<DialogService>();
        Services.AddSingleton<NotificationService>();
        Services.AddSingleton<TooltipService>();
    }

    [Fact]
    public void HealthcheckEditor_ShouldRenderHeadersAndFields()
    {
        var draft = new GameTypeRevisionHealthcheckDraft();

        var cut = Render<GameTypeRevisionHealthcheckEditor>(parameters => parameters
            .Add(p => p.Healthcheck, draft));

        Assert.Contains("Container Healthcheck", cut.Markup);
        Assert.Contains("Disable Healthcheck", cut.Markup);
        Assert.Contains("Start Period (Seconds)", cut.Markup);
        Assert.Contains("Interval (Seconds)", cut.Markup);
        Assert.Contains("Timeout (Seconds)", cut.Markup);
        Assert.Contains("Retries", cut.Markup);
    }

    [Fact]
    public void HealthcheckEditor_WhenValuesConfigured_ShowsEffectiveSummary()
    {
        var draft = new GameTypeRevisionHealthcheckDraft
        {
            StartPeriodSeconds = 300,
            IntervalSeconds = 30,
            TimeoutSeconds = 10,
            Retries = 3
        };

        var cut = Render<GameTypeRevisionHealthcheckEditor>(parameters => parameters
            .Add(p => p.Healthcheck, draft));

        Assert.Contains("Effective Healthcheck Configuration", cut.Markup);
        Assert.Contains("300s", cut.Markup);
        Assert.Contains("30s", cut.Markup);
        Assert.Contains("10s", cut.Markup);
    }

    [Fact]
    public void HealthcheckEditor_WhenDisabled_ShowsDisabledBadge()
    {
        var draft = new GameTypeRevisionHealthcheckDraft
        {
            Disable = true
        };

        var cut = Render<GameTypeRevisionHealthcheckEditor>(parameters => parameters
            .Add(p => p.Healthcheck, draft));

        Assert.Contains("Effective Healthcheck Configuration", cut.Markup);
        Assert.Contains("Disabled", cut.Markup);
        Assert.Contains("NONE", cut.Markup);
    }
}
