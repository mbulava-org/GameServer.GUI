using Bunit;
using GameServer.Web.Components.Pages.Servers;
using GameServer.Web.Models.V2;

namespace GameServer.Web.Tests.Components.Servers.V2;

public sealed class GameServerSettingFieldV2Tests : BunitContext
{
    public GameServerSettingFieldV2Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void BooleanSetting_ShouldRenderCheckbox()
    {
        var cut = Render<GameServerSettingFieldV2>(parameters => parameters
            .Add(p => p.Definition, new GameTypeSettingDefinition
            {
                SettingKey = "EULA",
                Metadata = new GameTypeSettingMetadata { DataType = "boolean" }
            })
            .Add(p => p.Value, "true"));

        Assert.Contains("Enabled", cut.Markup);
        Assert.Contains("input", cut.Markup);
    }

    [Fact]
    public void EnumSetting_WithInvalidMetadata_ShouldFallBackToTextBox()
    {
        var cut = Render<GameServerSettingFieldV2>(parameters => parameters
            .Add(p => p.Definition, new GameTypeSettingDefinition
            {
                SettingKey = "MODE",
                Metadata = new GameTypeSettingMetadata
                {
                    DataType = "enum",
                    AllowedValuesJson = "not-json"
                }
            })
            .Add(p => p.Value, "creative"));

        Assert.DoesNotContain("rz-dropdown", cut.Markup);
        Assert.Contains("input", cut.Markup);
    }
}