using Bunit;
using GameServer.Web.Components.Pages.GameTypes.Components.V2;

namespace GameServer.Web.Tests.Components.GameTypes.V2;

public sealed class GameTypeRevisionVolumesEditorTests : BunitContext
{
    public GameTypeRevisionVolumesEditorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void VolumesEditor_WhenEmpty_ShouldRenderEmptyMessage()
    {
        // Arrange & Act
        var cut = Render<GameTypeRevisionVolumesEditor>(parameters => parameters
            .Add(p => p.Volumes, [])
            .Add(p => p.VolumeUsageOptions, ["config", "data"])
            .Add(p => p.MountTypeOptions, [new VolumeMountTypeOption { Key = "volume", DisplayName = "Volume" }]));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("No volumes in this draft.", cut.Markup);
            Assert.Contains("Add Volume", cut.Markup);
        });
    }

    [Fact]
    public void VolumesEditor_WithVolumes_ShouldRenderFields()
    {
        // Arrange
        var volumes = new List<GameTypeRevisionVolumeDraft>
        {
            new()
            {
                Source = "/data",
                Usage = "data",
                MountType = "nfs",
                Permissions = "0755",
                EnsureNfsPathExists = true,
                ReadOnly = false
            }
        };

        // Act
        var cut = Render<GameTypeRevisionVolumesEditor>(parameters => parameters
            .Add(p => p.Volumes, volumes)
            .Add(p => p.VolumeUsageOptions, ["config", "data"])
            .Add(p => p.MountTypeOptions, [new VolumeMountTypeOption { Key = "nfs", DisplayName = "NFS Mount" }]));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Container Path", cut.Markup);
            Assert.Contains("/data", cut.Markup);
            Assert.Contains("Ensure NFS path exists", cut.Markup);
            Assert.Contains("Read-only", cut.Markup);
        });
    }

    [Fact]
    public void VolumesEditor_AddVolume_ShouldAddVolumeDraft()
    {
        // Arrange
        var volumes = new List<GameTypeRevisionVolumeDraft>();
        var draftChanged = false;

        var cut = Render<GameTypeRevisionVolumesEditor>(parameters => parameters
            .Add(p => p.Volumes, volumes)
            .Add(p => p.VolumeUsageOptions, ["config", "data"])
            .Add(p => p.MountTypeOptions, [new VolumeMountTypeOption { Key = "volume", DisplayName = "Volume" }])
            .Add(p => p.OnDraftChanged, () => draftChanged = true));

        // Act
        var addButton = cut.FindAll("button").First(b => b.TextContent.Contains("Add Volume"));
        addButton.Click();

        // Assert
        Assert.Single(volumes);
        Assert.True(draftChanged);
    }
}
