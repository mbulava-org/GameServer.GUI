using Bunit;
using GameServer.Web.Components.Pages.GameTypes.Components.V2;
using GameServer.Web.Models.V2;

namespace GameServer.Web.Tests.Components.GameTypes.V2;

public sealed class GameTypeRevisionReviewEditorTests : BunitContext
{
    public GameTypeRevisionReviewEditorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void GameTypeRevisionReviewEditor_ShouldRenderDraftSummaryAndDifferences()
    {
        // Arrange
        var detection = new GameTypeSetupDetectionResult
        {
            VersionTag = "latest",
            Ports = [ new DetectedPort { ContainerPort = 25565, Protocol = "tcp" } ],
            Settings = [ new DetectedSetting { Key = "SERVER_PORT", DefaultValue = "25565" } ],
            Volumes = [ new DetectedVolume { ContainerPath = "/data" } ]
        };

        var comparison = new GameTypeSetupComparisonResult
        {
            RevisionVersionTag = "1.21",
            HasChanges = true
        };

        // Act
        var cut = Render<GameTypeRevisionReviewEditor>(parameters => parameters
            .Add(p => p.RevisionVersionTag, "1.21.1")
            .Add(p => p.RevisionEnableTTY, true)
            .Add(p => p.RevisionIsPublished, false)
            .Add(p => p.PortCount, 2)
            .Add(p => p.VolumeCount, 1)
            .Add(p => p.SettingCount, 3)
            .Add(p => p.WebHostCount, 1)
            .Add(p => p.PortMappingCount, 2)
            .Add(p => p.DetectionResult, detection)
            .Add(p => p.DetectionComparison, comparison)
            .Add(p => p.ValidationIssues, new[] { "Revision version tag is required." })
            .Add(p => p.Warnings, new[] { "Revision notes are empty; consider recording what changed in this draft." })
            .Add(p => p.DraftDifferences, new[] { "Port count changed from 1 to 2." })
            .Add(p => p.DetailedDraftDifferences, new[] { "Added port '25566/udp'." }));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Revision Draft Summary", cut.Markup);
            Assert.Contains("1.21.1", cut.Markup);
            Assert.Contains("2 port(s)", cut.Markup);
            Assert.Contains("2 primary direct / default related port mapping rule(s)", cut.Markup);
            Assert.Contains("Primary mappings point directly at declared GameType ports.", cut.Markup);
            Assert.Contains("Compared to:", cut.Markup);
            Assert.Contains("Validation issues", cut.Markup);
            Assert.Contains("Detailed differences", cut.Markup);
        });
    }

    [Fact]
    public void GameTypeRevisionReviewEditor_WithoutDiffsOrDetection_ShouldRenderEmptyStateMessages()
    {
        // Act
        var cut = Render<GameTypeRevisionReviewEditor>(parameters => parameters
            .Add(p => p.PortCount, 0)
            .Add(p => p.VolumeCount, 0)
            .Add(p => p.SettingCount, 0)
            .Add(p => p.WebHostCount, 0)
            .Add(p => p.PortMappingCount, 0)
            .Add(p => p.ValidationIssues, Array.Empty<string>())
            .Add(p => p.Warnings, Array.Empty<string>())
            .Add(p => p.DraftDifferences, Array.Empty<string>())
            .Add(p => p.DetailedDraftDifferences, Array.Empty<string>()));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Version Tag:</strong> Not set", cut.Markup);
            Assert.Contains("No detection results have been applied.", cut.Markup);
            Assert.Contains("No draft differences detected from the selected revision.", cut.Markup);
        });
    }
}
