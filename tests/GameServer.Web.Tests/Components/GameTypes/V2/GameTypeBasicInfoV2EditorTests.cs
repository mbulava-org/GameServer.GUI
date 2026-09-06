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
    public async Task GameTypeBasicInfoV2Editor_WhenCallbacksTriggered_ShouldInvokeEventCallbacks()
    {
        // Arrange
        string? keyChanged = null;
        string? nameChanged = null;
        string? typeChanged = null;
        string? thumbChanged = null;
        string? docsChanged = null;
        string? descChanged = null;
        bool? activeChanged = null;

        var cut = Render<GameTypeBasicInfoV2Editor>(parameters => parameters
            .Add(p => p.IsNew, true)
            .Add(p => p.KeyValue, "mc")
            .Add(p => p.KeyValueChanged, EventCallback.Factory.Create<string>(this, v => keyChanged = v))
            .Add(p => p.DisplayName, "Minecraft")
            .Add(p => p.DisplayNameChanged, EventCallback.Factory.Create<string>(this, v => nameChanged = v))
            .Add(p => p.Type, "docker")
            .Add(p => p.TypeChanged, EventCallback.Factory.Create<string>(this, v => typeChanged = v))
            .Add(p => p.ThumbnailUrl, "https://example.com/thumb.png")
            .Add(p => p.ThumbnailUrlChanged, EventCallback.Factory.Create<string?>(this, v => thumbChanged = v))
            .Add(p => p.DocumentationUrl, "https://example.com/docs")
            .Add(p => p.DocumentationUrlChanged, EventCallback.Factory.Create<string?>(this, v => docsChanged = v))
            .Add(p => p.Description, "Desc")
            .Add(p => p.DescriptionChanged, EventCallback.Factory.Create<string?>(this, v => descChanged = v))
            .Add(p => p.IsActive, false)
            .Add(p => p.IsActiveChanged, EventCallback.Factory.Create<bool>(this, v => activeChanged = v)));

        // Act - invoke internal callbacks
        var instance = cut.Instance;
        var methodKey = instance.GetType().GetMethod("OnKeyValueChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodName = instance.GetType().GetMethod("OnDisplayNameChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodType = instance.GetType().GetMethod("OnTypeChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodThumb = instance.GetType().GetMethod("OnThumbnailUrlChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodDocs = instance.GetType().GetMethod("OnDocumentationUrlChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodDesc = instance.GetType().GetMethod("OnDescriptionChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodActive = instance.GetType().GetMethod("OnIsActiveChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        await (Task)methodKey!.Invoke(instance, ["mc-new"])!;
        await (Task)methodName!.Invoke(instance, ["Minecraft New"])!;
        await (Task)methodType!.Invoke(instance, ["docker-custom"])!;
        await (Task)methodType!.Invoke(instance, [""])!;
        await (Task)methodThumb!.Invoke(instance, ["https://example.com/new.png"])!;
        await (Task)methodDocs!.Invoke(instance, ["https://example.com/newdocs"])!;
        await (Task)methodDesc!.Invoke(instance, ["New Desc"])!;
        await (Task)methodActive!.Invoke(instance, [true])!;

        // Assert
        Assert.Equal("mc-new", keyChanged);
        Assert.Equal("Minecraft New", nameChanged);
        Assert.Equal("docker", typeChanged); // Empty string defaults to "docker"
        Assert.Equal("https://example.com/new.png", thumbChanged);
        Assert.Equal("https://example.com/newdocs", docsChanged);
        Assert.Equal("New Desc", descChanged);
        Assert.True(activeChanged);
    }
}
