using Bunit;
using GameServer.Web.Components.Pages.GameTypes.Components.V2;
using GameServer.Web.Models.V2;
using Microsoft.AspNetCore.Components;

namespace GameServer.Web.Tests.Components.GameTypes.V2;

public sealed class GameTypeRevisionDetectionEditorTests : BunitContext
{
    public GameTypeRevisionDetectionEditorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void GameTypeRevisionDetectionEditor_ShouldRenderDetectionSummaryAndMappingCounts()
    {
        // Arrange
        var detection = new GameTypeSetupDetectionResult
        {
            ImageReference = "itzg/minecraft-server",
            VersionTag = "latest",
            ImageDigest = "sha256:test",
            Ports = [ new DetectedPort { ContainerPort = 25565, Protocol = "tcp" } ],
            Settings =
            [
                new DetectedSetting
                {
                    Key = "SERVER_PORT",
                    DefaultValue = "25565",
                    PortMappings =
                    [
                        new DetectedSettingPortMapping
                        {
                            MappingRole = "Primary",
                            RelationType = "Direct",
                            TargetContainerPort = 25565,
                            TargetProtocol = "tcp"
                        }
                    ]
                }
            ],
            Volumes = [ new DetectedVolume { ContainerPath = "/data" } ]
        };

        // Act
        var cut = Render<GameTypeRevisionDetectionEditor>(parameters => parameters
            .Add(p => p.DetectionVersionTag, "latest")
            .Add(p => p.DetectionResult, detection));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Detection Result", cut.Markup);
            Assert.Contains("itzg/minecraft-server:latest", cut.Markup);
            Assert.Contains(">1<", cut.Markup);
        });
    }

    [Fact]
    public void GameTypeRevisionDetectionEditor_ShouldRenderComparisonDetails()
    {
        // Arrange
        var comparison = new GameTypeSetupComparisonResult
        {
            RevisionVersionTag = "1.21",
            HasChanges = true,
            DigestChanged = true,
            AddedPorts = [ "25566/udp" ],
            RemovedPorts = [ "25565/udp" ],
            AddedSettings = [ "QUERY_PORT" ],
            RemovedSettings = [ "OLD_PORT" ],
            AddedVolumes = [ "/config" ],
            RemovedVolumes = [ "/legacy" ],
            ChangedSettings =
            [
                new ChangedSetting
                {
                    Key = "SERVER_PORT",
                    RevisionValue = "25565",
                    DetectedValue = "25566"
                }
            ]
        };

        // Act
        var cut = Render<GameTypeRevisionDetectionEditor>(parameters => parameters
            .Add(p => p.DetectionVersionTag, "latest")
            .Add(p => p.DetectionResult, new GameTypeSetupDetectionResult())
            .Add(p => p.DetectionComparison, comparison));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Comparison vs Revision `1.21`", cut.Markup);
            Assert.Contains("Changes detected", cut.Markup);
            Assert.Contains("Added port:", cut.Markup);
            Assert.Contains("Changed Setting Defaults", cut.Markup);
        });
    }

    [Fact]
    public void GameTypeRevisionDetectionEditor_ShouldDisableActions_WhenScanUnavailable()
    {
        // Act
        var cut = Render<GameTypeRevisionDetectionEditor>(parameters => parameters
            .Add(p => p.IsNew, true)
            .Add(p => p.IsDetecting, true)
            .Add(p => p.DetectionVersionTag, "latest"));

        var buttons = cut.FindAll("button");

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.True(buttons.First(button => button.TextContent.Contains("Scan Tag", StringComparison.Ordinal)).HasAttribute("disabled"));
            Assert.True(buttons.First(button => button.TextContent.Contains("Apply All to Draft", StringComparison.Ordinal)).HasAttribute("disabled"));
        });
    }

    [Fact]
    public void GameTypeRevisionDetectionEditor_ClickingButtons_ShouldInvokeCallbacks()
    {
        // Arrange
        var detection = new GameTypeSetupDetectionResult
        {
            ImageReference = "itzg/minecraft-server",
            VersionTag = "latest"
        };

        var scanInvoked = false;
        var applyAllInvoked = false;
        var applyIdentityInvoked = false;
        var applyPortsInvoked = false;
        var applySettingsInvoked = false;
        var applyVolumesInvoked = false;

        // Act
        var cut = Render<GameTypeRevisionDetectionEditor>(parameters => parameters
            .Add(p => p.DetectionVersionTag, "latest")
            .Add(p => p.DetectionResult, detection)
            .Add(p => p.OnScanTag, EventCallback.Factory.Create(this, () => scanInvoked = true))
            .Add(p => p.OnApplyAll, EventCallback.Factory.Create(this, () => applyAllInvoked = true))
            .Add(p => p.OnApplyIdentity, EventCallback.Factory.Create(this, () => applyIdentityInvoked = true))
            .Add(p => p.OnApplyPorts, EventCallback.Factory.Create(this, () => applyPortsInvoked = true))
            .Add(p => p.OnApplySettings, EventCallback.Factory.Create(this, () => applySettingsInvoked = true))
            .Add(p => p.OnApplyVolumes, EventCallback.Factory.Create(this, () => applyVolumesInvoked = true)));

        cut.FindAll("button").First(button => button.TextContent.Contains("Scan Tag", StringComparison.Ordinal)).Click();
        cut.FindAll("button").First(button => button.TextContent.Contains("Apply All to Draft", StringComparison.Ordinal)).Click();
        cut.FindAll("button").First(button => button.TextContent.Contains("Apply Tag + Digest", StringComparison.Ordinal)).Click();
        cut.FindAll("button").First(button => button.TextContent.Contains("Apply Ports", StringComparison.Ordinal)).Click();
        cut.FindAll("button").First(button => button.TextContent.Contains("Apply Settings", StringComparison.Ordinal)).Click();
        cut.FindAll("button").First(button => button.TextContent.Contains("Apply Volumes", StringComparison.Ordinal)).Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.True(scanInvoked);
            Assert.True(applyAllInvoked);
            Assert.True(applyIdentityInvoked);
            Assert.True(applyPortsInvoked);
            Assert.True(applySettingsInvoked);
            Assert.True(applyVolumesInvoked);
        });
    }
}
