using Bunit;
using GameServer.Web.Components.Pages.GameTypes.Components.V2;
using GameServer.Web.Models.V2;

namespace GameServer.Web.Tests.Components.GameTypes.V2;

public sealed class GameTypeRevisionEditorTests : BunitContext
{
    public GameTypeRevisionEditorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void RevisionEditor_ShouldRenderFieldsAndGrid()
    {
        // Arrange
        var rows = new List<GameTypeRevisionListRow>
        {
            new()
            {
                Id = 1,
                VersionTag = "1.21.2",
                ImageDigest = "sha256:abc",
                IsPublished = true,
                EnableTTY = false,
                CreatedAt = DateTime.UtcNow,
                SourceRevision = new GameTypeRevision { Id = 1, VersionTag = "1.21.2", ReadyLogPattern = "Done (*)! For help*" }
            }
        };

        // Act
        var cut = Render<GameTypeRevisionEditor>(parameters => parameters
            .Add(p => p.RevisionRows, rows)
            .Add(p => p.VersionTag, "1.21.2")
            .Add(p => p.ImageReference, "itzg/minecraft-server")
            .Add(p => p.ReadyLogPattern, "Done (*)! For help*")
            .Add(p => p.Warnings, ["Image has no digest tag."])
            .Add(p => p.CanSetCurrentRevision, true));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Docker Image Reference", cut.Markup);
            Assert.Contains("itzg/minecraft-server", cut.Markup);
            Assert.Contains("Version Tag", cut.Markup);
            Assert.Contains("Ready Log Pattern (Optional)", cut.Markup);
            Assert.Contains("Done (*)! For help*", cut.Markup);
            Assert.Contains("Image has no digest tag.", cut.Markup);
            Assert.Contains("1.21.2", cut.Markup);
            Assert.Contains("Set Current", cut.Markup);
            Assert.Contains("Publish + Current", cut.Markup);
        });
    }

    [Fact]
    public void RevisionEditor_NewDraftClick_ShouldTriggerCallback()
    {
        // Arrange
        var newDraftClicked = false;
        var cut = Render<GameTypeRevisionEditor>(parameters => parameters
            .Add(p => p.RevisionRows, [])
            .Add(p => p.OnCreateNewDraft, () => newDraftClicked = true));

        // Act
        var button = cut.FindAll("button").First(b => b.TextContent.Contains("New Draft"));
        button.Click();

        // Assert
        Assert.True(newDraftClicked);
    }
}
