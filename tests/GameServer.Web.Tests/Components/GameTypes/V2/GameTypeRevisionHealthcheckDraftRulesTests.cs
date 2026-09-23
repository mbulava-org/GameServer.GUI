using GameServer.Web.Components.Pages.GameTypes.Components.V2;

namespace GameServer.Web.Tests.Components.GameTypes.V2;

public class GameTypeRevisionHealthcheckDraftRulesTests
{
    [Fact]
    public void Presets_ShouldContainStandardProfiles()
    {
        var presets = GameTypeRevisionHealthcheckDraftRules.Presets;
        Assert.NotEmpty(presets);
        Assert.Contains(presets, p => p.Name.Contains("Slow Starting", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(presets, p => p.Name.Contains("Heavy Modded", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(presets, p => p.Name.Contains("Disable Healthcheck", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateHealthcheck_WhenDraftNull_ReturnsNoIssues()
    {
        var issues = GameTypeRevisionHealthcheckDraftRules.ValidateHealthcheck(null);
        Assert.Empty(issues);
    }

    [Fact]
    public void ValidateHealthcheck_WhenDisabled_ReturnsNoIssuesEvenIfTimingNegative()
    {
        var draft = new GameTypeRevisionHealthcheckDraft
        {
            Disable = true,
            StartPeriodSeconds = -10,
            IntervalSeconds = -5
        };

        var issues = GameTypeRevisionHealthcheckDraftRules.ValidateHealthcheck(draft);
        Assert.Empty(issues);
    }

    [Fact]
    public void ValidateHealthcheck_WhenStartPeriodNegative_ReturnsIssue()
    {
        var draft = new GameTypeRevisionHealthcheckDraft
        {
            StartPeriodSeconds = -1
        };

        var issues = GameTypeRevisionHealthcheckDraftRules.ValidateHealthcheck(draft);
        Assert.Contains(issues, i => i.Contains("start period cannot be negative", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateHealthcheck_WhenIntervalLessThanOne_ReturnsIssue()
    {
        var draft = new GameTypeRevisionHealthcheckDraft
        {
            IntervalSeconds = 0
        };

        var issues = GameTypeRevisionHealthcheckDraftRules.ValidateHealthcheck(draft);
        Assert.Contains(issues, i => i.Contains("interval must be at least 1 second", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateHealthcheck_WhenTimeoutExceedsInterval_ReturnsIssue()
    {
        var draft = new GameTypeRevisionHealthcheckDraft
        {
            IntervalSeconds = 10,
            TimeoutSeconds = 15
        };

        var issues = GameTypeRevisionHealthcheckDraftRules.ValidateHealthcheck(draft);
        Assert.Contains(issues, i => i.Contains("should not exceed interval", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateHealthcheck_WhenValidValues_ReturnsNoIssues()
    {
        var draft = new GameTypeRevisionHealthcheckDraft
        {
            StartPeriodSeconds = 300,
            IntervalSeconds = 30,
            TimeoutSeconds = 10,
            Retries = 3
        };

        var issues = GameTypeRevisionHealthcheckDraftRules.ValidateHealthcheck(draft);
        Assert.Empty(issues);
    }

    [Fact]
    public void HasAnyValue_WhenEmpty_ReturnsFalse()
    {
        var draft = new GameTypeRevisionHealthcheckDraft();
        Assert.False(draft.HasAnyValue());
    }

    [Fact]
    public void HasAnyValue_WhenStartPeriodSet_ReturnsTrue()
    {
        var draft = new GameTypeRevisionHealthcheckDraft { StartPeriodSeconds = 120 };
        Assert.True(draft.HasAnyValue());
    }
}
