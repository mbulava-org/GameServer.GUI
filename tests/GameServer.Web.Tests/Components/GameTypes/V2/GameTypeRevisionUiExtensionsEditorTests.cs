using System.Collections.Generic;
using System.Linq;
using Bunit;
using GameServer.Web.Components.Pages.GameTypes.Components.V2;
using Microsoft.Extensions.DependencyInjection;
using Radzen;
using Radzen.Blazor;
using Xunit;

namespace GameServer.Web.Tests.Components.GameTypes.V2;

public sealed class GameTypeRevisionUiExtensionsEditorTests : BunitContext
{
    public GameTypeRevisionUiExtensionsEditorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<DialogService>();
        Services.AddSingleton<NotificationService>();
        Services.AddSingleton<TooltipService>();
    }

    [Fact]
    public void UiExtensionsEditor_ShouldRenderAddButtons()
    {
        var extensions = new List<GameTypeRevisionUiExtensionDraft>();

        var cut = Render<GameTypeRevisionUiExtensionsEditor>(parameters => parameters
            .Add(p => p.Extensions, extensions));

        Assert.Contains("Add RCON Extension", cut.Markup);
        Assert.Contains("Add Extension", cut.Markup);
        Assert.Contains("No UI extensions declared for this draft", cut.Markup);
    }

    [Fact]
    public void UiExtensionsEditor_WhenAddRconClicked_ShouldAddRconExtensionWithEmptyParameters()
    {
        var extensions = new List<GameTypeRevisionUiExtensionDraft>();
        var settings = new List<GameTypeRevisionSettingDraft>
        {
            new() { SettingKey = "RCON_PORT", Metadata = new() { DataType = "number" } },
            new() { SettingKey = "RCON_ENABLED", Metadata = new() { DataType = "boolean" } },
            new() { SettingKey = "ADMIN_PASSWORD", Metadata = new() { DataType = "string" } }
        };

        var cut = Render<GameTypeRevisionUiExtensionsEditor>(parameters => parameters
            .Add(p => p.Extensions, extensions)
            .Add(p => p.AvailableSettings, settings));

        // Click Add RCON Extension button
        var addRconBtn = cut.FindComponents<RadzenButton>()
            .First(b => b.Instance.Text == "Add RCON Extension");

        addRconBtn.Find("button").Click();

        Assert.Single(extensions);
        var rconExt = extensions[0];
        Assert.Equal("RCON", rconExt.Title);
        Assert.Equal("GameServer.Web.Components.Server.Extensions.RconTab", rconExt.ComponentTypeName);
        Assert.Equal("GameServer.Web", rconExt.AssemblyName);
        Assert.Equal("terminal", rconExt.Icon);

        // Parameters MUST NOT be defaulted; they start empty until explicitly configured
        Assert.Empty(rconExt.Parameters);

        // Verify RCON configuration panel renders
        Assert.Contains("RCON Configuration", cut.Markup);
        Assert.Contains("Port Setting", cut.Markup);
        Assert.Contains("Or Hardcoded Port", cut.Markup);
        Assert.Contains("Password Setting", cut.Markup);
        Assert.Contains("Or Hardcoded Password", cut.Markup);
        Assert.Contains("Enable Setting", cut.Markup);
        Assert.Contains("Preset Buttons", cut.Markup);
    }

    [Fact]
    public void UiExtensionsEditor_WhenPortSettingSelected_ShouldSetPortSettingKey()
    {
        var rconExt = new GameTypeRevisionUiExtensionDraft
        {
            Title = "RCON",
            ComponentTypeName = "GameServer.Web.Components.Server.Extensions.RconTab",
            AssemblyName = "GameServer.Web",
            Icon = "terminal",
            Parameters = []
        };
        var extensions = new List<GameTypeRevisionUiExtensionDraft> { rconExt };
        var settings = new List<GameTypeRevisionSettingDraft>
        {
            new() { SettingKey = "RCON_PORT", Metadata = new() { DataType = "number" } }
        };

        var cut = Render<GameTypeRevisionUiExtensionsEditor>(parameters => parameters
            .Add(p => p.Extensions, extensions)
            .Add(p => p.AvailableSettings, settings));

        var dropDowns = cut.FindComponents<RadzenDropDown<string>>();
        var portDropDown = dropDowns.First();

        // Select RCON_PORT
        portDropDown.Instance.SelectItem("RCON_PORT");

        Assert.Single(rconExt.Parameters);
        Assert.Equal("portSettingKey", rconExt.Parameters[0].Key);
        Assert.Equal("RCON_PORT", rconExt.Parameters[0].Value);
    }

    [Fact]
    public void UiExtensionsEditor_WhenPasswordSettingSelected_ShouldSetPasswordSettingKey()
    {
        var rconExt = new GameTypeRevisionUiExtensionDraft
        {
            Title = "RCON",
            ComponentTypeName = "GameServer.Web.Components.Server.Extensions.RconTab",
            AssemblyName = "GameServer.Web",
            Icon = "terminal",
            Parameters = []
        };
        var extensions = new List<GameTypeRevisionUiExtensionDraft> { rconExt };
        var settings = new List<GameTypeRevisionSettingDraft>
        {
            new() { SettingKey = "ADMIN_PASSWORD", Metadata = new() { DataType = "string" } }
        };

        var cut = Render<GameTypeRevisionUiExtensionsEditor>(parameters => parameters
            .Add(p => p.Extensions, extensions)
            .Add(p => p.AvailableSettings, settings));

        var dropDowns = cut.FindComponents<RadzenDropDown<string>>();
        var passwordDropDown = dropDowns[1]; // second dropdown is Password Setting

        passwordDropDown.Instance.SelectItem("ADMIN_PASSWORD");

        Assert.Single(rconExt.Parameters);
        Assert.Equal("passwordSettingKey", rconExt.Parameters[0].Key);
        Assert.Equal("ADMIN_PASSWORD", rconExt.Parameters[0].Value);
    }
}
