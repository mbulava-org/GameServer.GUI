using Bunit;
using GameServer.Web.Components.Pages.Servers;
using GameServer.Web.Models.V2;
using Radzen.Blazor;

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

    [Fact]
    public void ServerVariableSetting_ShouldRenderToggleAndEncodeValueWhenEnabled()
    {
        string? saved = null;

        var cut = Render<GameServerSettingFieldV2>(parameters => parameters
            .Add(p => p.Definition, CreateServerVariableDefinition())
            .Add(p => p.Value, "{Name} world")
            .Add(p => p.ValueChanged, value => saved = value));

        var toggle = cut.FindComponent<RadzenSwitch>();
        cut.InvokeAsync(() => toggle.Instance.Change.InvokeAsync(true)).GetAwaiter().GetResult();

        Assert.Equal("@vars:{Name} world", saved);
    }

    [Fact]
    public void ServerVariableSetting_ShouldDecodeStoredValueIntoRawTextAndToggleState()
    {
        var cut = Render<GameServerSettingFieldV2>(parameters => parameters
            .Add(p => p.Definition, CreateServerVariableDefinition())
            .Add(p => p.Value, "@vars:{ServerId}"));

        Assert.True(cut.FindComponent<RadzenSwitch>().Instance.Value);
        Assert.Equal("{ServerId}", cut.FindComponent<RadzenTextBox>().Instance.Value);
    }

    [Fact]
    public void ServerVariableSetting_WhenToggleDisabled_ShouldStoreLiteralValue()
    {
        string? saved = null;

        var cut = Render<GameServerSettingFieldV2>(parameters => parameters
            .Add(p => p.Definition, CreateServerVariableDefinition())
            .Add(p => p.Value, "@vars:{Name}")
            .Add(p => p.ValueChanged, value => saved = value));

        var toggle = cut.FindComponent<RadzenSwitch>();
        cut.InvokeAsync(() => toggle.Instance.Change.InvokeAsync(false)).GetAwaiter().GetResult();

        Assert.Equal("{Name}", saved);
    }

    [Fact]
    public void PortSetting_ShouldRenderNumericWithCurrentValue()
    {
        var cut = Render<GameServerSettingFieldV2>(parameters => parameters
            .Add(p => p.Definition, CreatePortDefinition())
            .Add(p => p.Value, "25565"));

        Assert.Equal(25565, cut.FindComponent<RadzenNumeric<int>>().Instance.Value);
    }

    [Fact]
    public void PortSetting_WhenChanged_ShouldStoreInvariantNumericString()
    {
        string? saved = null;

        var cut = Render<GameServerSettingFieldV2>(parameters => parameters
            .Add(p => p.Definition, CreatePortDefinition())
            .Add(p => p.Value, "25565")
            .Add(p => p.ValueChanged, value => saved = value));

        var numeric = cut.FindComponent<RadzenNumeric<int>>();
        cut.InvokeAsync(() => numeric.Instance.ValueChanged.InvokeAsync(25570)).GetAwaiter().GetResult();

        Assert.Equal("25570", saved);
    }

    [Fact]
    public void PortSetting_WithValidationMessage_ShouldRenderTheMessage()
    {
        var cut = Render<GameServerSettingFieldV2>(parameters => parameters
            .Add(p => p.Definition, CreatePortDefinition())
            .Add(p => p.Value, "25565")
            .Add(p => p.ValidationMessage, "Port '25565/tcp' is already in use by another managed server."));

        Assert.Contains("already in use by another managed server", cut.Markup);
        Assert.Contains("text-danger", cut.Markup);
    }

    private static GameTypeSettingDefinition CreatePortDefinition()
    {
        return new GameTypeSettingDefinition
        {
            SettingKey = "SERVER_PORT",
            Metadata = new GameTypeSettingMetadata
            {
                DataType = "port",
                PortMappings =
                [
                    new GameTypeSettingPortMapping
                    {
                        MappingRole = "Primary",
                        RelationType = "Direct",
                        TargetContainerPort = 25565,
                        TargetProtocol = "tcp"
                    }
                ]
            }
        };
    }

    private static GameTypeSettingDefinition CreateServerVariableDefinition()
    {
        return new GameTypeSettingDefinition
        {
            SettingKey = "MOTD",
            Metadata = new GameTypeSettingMetadata
            {
                DataType = ServerVariableSetting.DataType
            }
        };
    }
}