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

    [Fact]
    public async Task VolumesEditor_RemoveMoveAndVariableChanged_ShouldUpdateCorrectly()
    {
        // Arrange
        var v1 = new GameTypeRevisionVolumeDraft { Source = "/data", MountType = "nfs", Usage = "data", Required = false, OwnerUid = 1000, OwnerGid = 1000 };
        var v2 = new GameTypeRevisionVolumeDraft { Source = "/config", MountType = "volume", Usage = "config", Required = true };
        var volumes = new List<GameTypeRevisionVolumeDraft> { v1, v2 };
        var draftChanged = 0;

        var cut = Render<GameTypeRevisionVolumesEditor>(parameters => parameters
            .Add(p => p.Volumes, volumes)
            .Add(p => p.VolumeUsageOptions, ["config", "data"])
            .Add(p => p.MountTypeOptions, [new VolumeMountTypeOption { Key = "nfs", DisplayName = "NFS Mount", DefaultReadOnly = true, DefaultEnsureNfsPathExists = true, DefaultOwnerUid = 1001, DefaultOwnerGid = 1001, DefaultPermissions = "0750" }])
            .Add(p => p.NumericVariableOptions, [new VolumeNumericVariableOption { Label = "User ID", SettingKey = "UID_VAR" }])
            .Add(p => p.OnDraftChanged, () => draftChanged++));

        var instance = cut.Instance;
        var methodMove = instance.GetType().GetMethod("MoveVolumeDraftAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodRemove = instance.GetType().GetMethod("RemoveVolumeDraftAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodUid = instance.GetType().GetMethod("OnUidVariableChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodGid = instance.GetType().GetMethod("OnGidVariableChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodAdd = instance.GetType().GetMethod("AddVolumeDraft", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Move v1 down
        await (Task)methodMove!.Invoke(instance, [v1, 1])!;
        Assert.Equal(v2, volumes[0]);
        Assert.Equal(v1, volumes[1]);

        // Change UID / GID variables
        await (Task)methodUid!.Invoke(instance, [v1, "UID_VAR"])!;
        Assert.Equal("UID_VAR", v1.OwnerUidVariable);
        Assert.Null(v1.OwnerUid);

        await (Task)methodGid!.Invoke(instance, [v1, "GID_VAR"])!;
        Assert.Equal("GID_VAR", v1.OwnerGidVariable);
        Assert.Null(v1.OwnerGid);

        // Clear UID / GID variables
        await (Task)methodUid.Invoke(instance, [v1, ""])!;
        Assert.Null(v1.OwnerUidVariable);

        await (Task)methodGid.Invoke(instance, [v1, null])!;
        Assert.Null(v1.OwnerGidVariable);

        // Add volume with defaults from MountTypeOptions
        await (Task)methodAdd!.Invoke(instance, [])!;
        Assert.Equal(3, volumes.Count);
        var added = volumes.Last();
        Assert.True(added.ReadOnly);
        Assert.True(added.EnsureNfsPathExists);
        Assert.Equal(1001, added.OwnerUid);
        Assert.Equal("0750", added.Permissions);

        // Remove volume
        await (Task)methodRemove!.Invoke(instance, [v2])!;
        Assert.Equal(2, volumes.Count);
        Assert.DoesNotContain(v2, volumes);

        Assert.True(draftChanged > 0);
    }
}
