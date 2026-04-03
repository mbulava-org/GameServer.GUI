using Bunit;
using GameServer.Web.Components.Pages.GameTypes.Components.V2;

namespace GameServer.Web.Tests.Components.GameTypes.V2;

public sealed class GameTypeRevisionSettingsEditorTests : BunitContext
{
    public GameTypeRevisionSettingsEditorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void GameTypeRevisionSettingsEditor_ShouldSelectClickedSetting()
    {
        // Arrange
        var settings = new List<GameTypeRevisionSettingDraft>
        {
            new()
            {
                SettingKey = "EULA",
                DefaultValue = "TRUE",
                Description = "Accept the license",
                Metadata = new GameTypeRevisionSettingMetadataDraft { Category = "General", DataType = "boolean" }
            },
            new()
            {
                SettingKey = "SERVER_PORT",
                DefaultValue = "25565",
                Description = "Primary connection port",
                Metadata = new GameTypeRevisionSettingMetadataDraft { Category = "Network", DataType = "port" }
            }
        };

        // Act
        var cut = Render<GameTypeRevisionSettingsEditor>(parameters => parameters
            .Add(p => p.Settings, settings)
            .Add(p => p.DefinedPorts, new List<GameTypeRevisionPortDraft>
            {
                new() { ContainerPort = 25565, Protocol = "tcp", AdvertisedPort = true }
            })
            .Add(p => p.DataTypeOptions, new[] { "string", "number", "boolean", "enum", "port" })
            .Add(p => p.ProtocolOptions, new[] { "tcp", "udp" })
            .Add(p => p.PortMappingRoleOptions, new[] { "Primary", "Related" })
            .Add(p => p.PortRelationTypeOptions, new[] { "Direct", "Offset", "Fixed", "Multiplier" }));

        cut.FindAll(".setting-list-item")[1].Click();

        // Assert
        cut.WaitForAssertion(() => Assert.Contains("SERVER_PORT", cut.Find(".setting-key-large").TextContent));
    }

    [Fact]
    public void GameTypeRevisionSettingsEditor_AddSetting_ShouldAppendNewSettingAndSelectIt()
    {
        // Arrange
        var settings = new List<GameTypeRevisionSettingDraft>
        {
            new()
            {
                SettingKey = "EULA",
                DefaultValue = "TRUE",
                Metadata = new GameTypeRevisionSettingMetadataDraft { Category = "General", DataType = "boolean" }
            }
        };

        // Act
        var cut = Render<GameTypeRevisionSettingsEditor>(parameters => parameters
            .Add(p => p.Settings, settings)
            .Add(p => p.DefinedPorts, new List<GameTypeRevisionPortDraft>
            {
                new() { ContainerPort = 25565, Protocol = "tcp", AdvertisedPort = true }
            })
            .Add(p => p.DataTypeOptions, new[] { "string", "number", "boolean", "enum", "port" })
            .Add(p => p.ProtocolOptions, new[] { "tcp", "udp" })
            .Add(p => p.PortMappingRoleOptions, new[] { "Primary", "Related" })
            .Add(p => p.PortRelationTypeOptions, new[] { "Direct", "Offset", "Fixed", "Multiplier" }));

        cut.FindAll("button").First(button => button.TextContent.Contains("Add Setting", StringComparison.Ordinal)).Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, settings.Count);
            Assert.Contains("NEW_SETTING_2", cut.Find(".setting-key-large").TextContent);
        });
    }

    [Fact]
    public void GameTypeRevisionSettingsEditor_AddPortMapping_ShouldBeDisabled_WhenNoDefinedPortsExist()
    {
        // Arrange
        var settings = new List<GameTypeRevisionSettingDraft>
        {
            new()
            {
                SettingKey = "SERVER_PORT",
                DefaultValue = "25565",
                Metadata = new GameTypeRevisionSettingMetadataDraft { Category = "Network", DataType = "port" }
            }
        };

        // Act
        var cut = Render<GameTypeRevisionSettingsEditor>(parameters => parameters
            .Add(p => p.Settings, settings)
            .Add(p => p.DefinedPorts, Array.Empty<GameTypeRevisionPortDraft>())
            .Add(p => p.DataTypeOptions, new[] { "string", "number", "boolean", "enum", "port" })
            .Add(p => p.ProtocolOptions, new[] { "tcp", "udp" })
            .Add(p => p.PortMappingRoleOptions, new[] { "Primary", "Related" })
            .Add(p => p.PortRelationTypeOptions, new[] { "Direct", "Offset", "Fixed", "Multiplier" }));

        cut.Find(".setting-list-item").Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Define at least one port/protocol in the Ports tab", cut.Markup);
            Assert.True(cut.FindAll("button").First(button => button.TextContent.Contains("Add Port Mapping", StringComparison.Ordinal)).HasAttribute("disabled"));
        });
    }
}
