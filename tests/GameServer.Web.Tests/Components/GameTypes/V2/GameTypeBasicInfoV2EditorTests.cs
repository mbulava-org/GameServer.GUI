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
}
