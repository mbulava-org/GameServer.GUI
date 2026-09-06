using Bunit;
using GameServer.Web.Components.Pages.GameTypes.Components.V2;

namespace GameServer.Web.Tests.Components.GameTypes.V2;

public sealed class GameTypeRevisionUiExtensionsEditorTests : BunitContext
{
    public GameTypeRevisionUiExtensionsEditorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void UiExtensionsEditor_WhenEmpty_ShouldRenderEmptyMessage()
    {
        // Act
        var cut = Render<GameTypeRevisionUiExtensionsEditor>(parameters => parameters
            .Add(p => p.Extensions, []));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("No UI extensions declared for this draft.", cut.Markup);
            Assert.Contains("Add Extension", cut.Markup);
        });
    }

    [Fact]
    public void UiExtensionsEditor_WithExtensions_ShouldRenderFields()
    {
        // Arrange
        var extensions = new List<GameTypeRevisionUiExtensionDraft>
        {
            new()
            {
                Title = "Palworld API",
                ComponentTypeName = "GameServer.Web.Components.Server.Extensions.PalworldApiTab",
                Order = 1,
                AssemblyName = "GameServer.Web",
                Icon = "dashboard",
                Parameters =
                [
                    new() { Key = "Endpoint", Value = "/v1/api" }
                ]
            }
        };

        // Act
        var cut = Render<GameTypeRevisionUiExtensionsEditor>(parameters => parameters
            .Add(p => p.Extensions, extensions));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Title", cut.Markup);
            Assert.Contains("Palworld API", cut.Markup);
            Assert.Contains("GameServer.Web.Components.Server.Extensions.PalworldApiTab", cut.Markup);
            Assert.Contains("Parameters", cut.Markup);
            Assert.Contains("Endpoint", cut.Markup);
            Assert.Contains("/v1/api", cut.Markup);
        });
    }

    [Fact]
    public void UiExtensionsEditor_AddExtension_ShouldAddDraftAndNotify()
    {
        // Arrange
        var extensions = new List<GameTypeRevisionUiExtensionDraft>();
        var changed = false;

        var cut = Render<GameTypeRevisionUiExtensionsEditor>(parameters => parameters
            .Add(p => p.Extensions, extensions)
            .Add(p => p.OnDraftChanged, () => changed = true));

        // Act
        var addButton = cut.FindAll("button").First(b => b.TextContent.Contains("Add Extension"));
        addButton.Click();

        // Assert
        Assert.Single(extensions);
        Assert.Equal(0, extensions[0].Order);
        Assert.True(changed);
    }

    [Fact]
    public void UiExtensionsEditor_RemoveExtension_ShouldRemoveAndNotify()
    {
        // Arrange
        var ext = new GameTypeRevisionUiExtensionDraft { Title = "Test Ext" };
        var extensions = new List<GameTypeRevisionUiExtensionDraft> { ext };
        var changed = false;

        var cut = Render<GameTypeRevisionUiExtensionsEditor>(parameters => parameters
            .Add(p => p.Extensions, extensions)
            .Add(p => p.OnDraftChanged, () => changed = true));

        // Act
        var removeButton = cut.FindAll("button").First(b => b.TextContent.Contains("Remove"));
        removeButton.Click();

        // Assert
        Assert.Empty(extensions);
        Assert.True(changed);
    }

    [Fact]
    public void UiExtensionsEditor_AddAndRemoveParameter_ShouldModifyListAndNotify()
    {
        // Arrange
        var ext = new GameTypeRevisionUiExtensionDraft { Title = "Test Ext" };
        var extensions = new List<GameTypeRevisionUiExtensionDraft> { ext };
        var changeCount = 0;

        var cut = Render<GameTypeRevisionUiExtensionsEditor>(parameters => parameters
            .Add(p => p.Extensions, extensions)
            .Add(p => p.OnDraftChanged, () => changeCount++));

        // Act - Add Parameter
        var addParamButton = cut.FindAll("button").First(b => b.TextContent.Contains("Add Parameter"));
        addParamButton.Click();

        Assert.Single(ext.Parameters);
        Assert.Equal(1, changeCount);

        // Act - Remove Parameter
        var removeParamButton = cut.FindAll("button").First(b => b.InnerHtml.Contains("close") || b.TextContent.Contains("close") || b.Attributes.Any(a => a.Value.Contains("close")));
        removeParamButton.Click();

        Assert.Empty(ext.Parameters);
        Assert.Equal(2, changeCount);
    }

    [Fact]
    public void UiExtensionsEditor_AtMaxExtensions_ShouldShowWarningAndDisableAdd()
    {
        // Arrange
        var extensions = Enumerable.Range(0, GameTypeRevisionUiExtensionsEditor.MaxExtensions)
            .Select(i => new GameTypeRevisionUiExtensionDraft { Title = $"Ext {i}", Order = i })
            .ToList();

        // Act
        var cut = Render<GameTypeRevisionUiExtensionsEditor>(parameters => parameters
            .Add(p => p.Extensions, extensions));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains($"Maximum of {GameTypeRevisionUiExtensionsEditor.MaxExtensions} extensions per revision.", cut.Markup);
            var addButton = cut.FindAll("button").First(b => b.TextContent.Contains("Add Extension"));
            Assert.True(addButton.HasAttribute("disabled"));
        });
    }
}
