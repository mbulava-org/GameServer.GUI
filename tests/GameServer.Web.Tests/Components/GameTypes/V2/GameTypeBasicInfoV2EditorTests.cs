using Bunit;
using GameServer.Web.Components.Pages.GameTypes.Components.V2;
using Microsoft.AspNetCore.Components;

namespace GameServer.Web.Tests.Components.GameTypes.V2;

public sealed class GameTypeBasicInfoV2EditorTests : BunitContext
{
    public GameTypeBasicInfoV2EditorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void GameTypeBasicInfoV2Editor_ShouldRenderFields()
    {
        // Arrange & Act
        var cut = Render<GameTypeBasicInfoV2Editor>(parameters => parameters
            .Add(p => p.IsNew, true)
            .Add(p => p.KeyValue, "minecraft")
            .Add(p => p.DisplayName, "Minecraft")
            .Add(p => p.Type, "docker")
            .Add(p => p.ThumbnailUrl, "https://example.com/mc.png")
            .Add(p => p.DocumentationUrl, "https://example.com/docs")
            .Add(p => p.Description, "Minecraft Server")
            .Add(p => p.IsActive, true));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Key", cut.Markup);
            Assert.Contains("Display Name", cut.Markup);
            Assert.Contains("Thumbnail URL", cut.Markup);
            Assert.Contains("Documentation URL", cut.Markup);
            Assert.Contains("Active", cut.Markup);
            Assert.Contains("Description", cut.Markup);
        });
    }

    [Fact]
    public void GameTypeBasicInfoV2Editor_WhenNotNew_ShouldDisableKeyField()
    {
        // Arrange & Act
        var cut = Render<GameTypeBasicInfoV2Editor>(parameters => parameters
            .Add(p => p.IsNew, false)
            .Add(p => p.KeyValue, "minecraft")
            .Add(p => p.DisplayName, "Minecraft"));

        // Assert
        cut.WaitForAssertion(() =>
        {
            var keyInput = cut.Find("input");
            Assert.True(keyInput.HasAttribute("disabled"));
        });
    }

    [Fact]
    public void GameTypeBasicInfoV2Editor_ShouldRenderMaxLengthAttributes()
    {
        // Arrange & Act
        var cut = Render<GameTypeBasicInfoV2Editor>(parameters => parameters
            .Add(p => p.IsNew, true)
            .Add(p => p.KeyValue, "valheim")
            .Add(p => p.DisplayName, "Valheim"));

        // Assert
        cut.WaitForAssertion(() =>
        {
            var inputs = cut.FindAll("input");
            Assert.Contains(inputs, i => i.GetAttribute("maxlength") == "100");
            Assert.Contains(inputs, i => i.GetAttribute("maxlength") == "200");
            Assert.Contains(inputs, i => i.GetAttribute("maxlength") == "500");
        });
    }

    [Fact]
    public void GameTypeBasicInfoV2Editor_WhenValidationIssuesProvided_ShouldRenderAlert()
    {
        // Arrange & Act
        var cut = Render<GameTypeBasicInfoV2Editor>(parameters => parameters
            .Add(p => p.IsNew, true)
            .Add(p => p.ValidationIssues, new[] { "Thumbnail URL cannot exceed 500 characters." }));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Basic information validation", cut.Markup);
            Assert.Contains("Thumbnail URL cannot exceed 500 characters.", cut.Markup);
        });
    }
}
