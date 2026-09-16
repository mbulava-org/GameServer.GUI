using GameServer.Web.Components.Pages.GameTypes.Components.V2;
using GameServer.Web.Models.V2;
using GameServer.Web.Tests.Helpers;

namespace GameServer.Web.Tests.Components.GameTypes.V2;

public class GameTypeRevisionResourcesDraftRulesTests
{
    [Fact]
    public void Presets_ShouldContainStandardProfiles()
    {
        var presets = GameTypeRevisionResourcesDraftRules.Presets;
        Assert.NotEmpty(presets);
        Assert.Contains(presets, p => p.Name.StartsWith("Standard", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(presets, p => p.Name.StartsWith("Light", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(presets, p => p.Name.StartsWith("High Performance", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(presets, p => p.Name.StartsWith("Dedicated Node", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ConvertFromBytes_ShouldConvertCorrectly()
    {
        var (val1, unit1) = GameTypeRevisionResourcesDraftRules.ConvertFromBytes(4L * 1024 * 1024 * 1024);
        Assert.Equal(4, val1);
        Assert.Equal("GB", unit1);

        var (val2, unit2) = GameTypeRevisionResourcesDraftRules.ConvertFromBytes(512L * 1024 * 1024);
        Assert.Equal(512, val2);
        Assert.Equal("MB", unit2);
    }

    [Fact]
    public void ConvertToBytes_ShouldConvertCorrectly()
    {
        Assert.Equal(2L * 1024 * 1024 * 1024, GameTypeRevisionResourcesDraftRules.ConvertToBytes(2, "GB"));
        Assert.Equal(512L * 1024 * 1024, GameTypeRevisionResourcesDraftRules.ConvertToBytes(512, "MB"));
        Assert.Null(GameTypeRevisionResourcesDraftRules.ConvertToBytes(null, "GB"));
        Assert.Null(GameTypeRevisionResourcesDraftRules.ConvertToBytes(0, "GB"));
    }

    [Fact]
    public void ValidateResources_WhenCpuReservationExceedsLimit_ShouldReturnError()
    {
        var draft = new GameTypeRevisionResourcesDraft
        {
            CpuLimitCores = 1.0m,
            CpuReservationCores = 2.0m
        };

        var issues = GameTypeRevisionResourcesDraftRules.ValidateResources(draft);
        Assert.Contains(issues, i => i.Contains("cannot exceed CPU limit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateResources_WhenMemoryReservationExceedsLimit_ShouldReturnError()
    {
        var draft = new GameTypeRevisionResourcesDraft
        {
            MemoryLimitValue = 1024,
            MemoryLimitUnit = "MB",
            MemoryReservationValue = 2,
            MemoryReservationUnit = "GB"
        };

        var issues = GameTypeRevisionResourcesDraftRules.ValidateResources(draft);
        Assert.Contains(issues, i => i.Contains("cannot exceed Memory limit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateResources_WhenEmptyConstraintTargetOrValue_ShouldReturnError()
    {
        var draft = new GameTypeRevisionResourcesDraft();
        draft.Constraints.Add(new PlacementConstraintDraft
        {
            TargetType = "custom",
            CustomTarget = "",
            Value = ""
        });

        var issues = GameTypeRevisionResourcesDraftRules.ValidateResources(draft);
        Assert.Contains(issues, i => i.Contains("Target is required", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, i => i.Contains("Value is required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateResources_WhenEmptyDraft_ShouldReturnNoErrors()
    {
        var draft = new GameTypeRevisionResourcesDraft();
        var issues = GameTypeRevisionResourcesDraftRules.ValidateResources(draft);
        Assert.Empty(issues);
    }

    [Fact]
    public void PlacementConstraintDraft_GetComputedExpression_ShouldFormatCorrectly()
    {
        var c1 = new PlacementConstraintDraft
        {
            TargetType = "node.role",
            Operator = "==",
            Value = "worker"
        };
        Assert.Equal("node.role == worker", c1.GetComputedExpression());

        var c2 = new PlacementConstraintDraft
        {
            TargetType = "custom",
            CustomTarget = "engine.labels.os",
            Operator = "!=",
            Value = "windows"
        };
        Assert.Equal("engine.labels.os != windows", c2.GetComputedExpression());
    }
}
