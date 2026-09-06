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
            .Add(p => p.DataTypeOptions, new[] { "string", "number", "boolean", "yesno", "enum", "port" })
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
            .Add(p => p.DataTypeOptions, new[] { "string", "number", "boolean", "yesno", "enum", "port" })
            .Add(p => p.ProtocolOptions, new[] { "tcp", "udp" })
            .Add(p => p.PortMappingRoleOptions, new[] { "Primary", "Related" })
            .Add(p => p.PortRelationTypeOptions, new[] { "Direct", "Offset", "Fixed", "Multiplier" }));

        cut.FindAll("button").First(button => button.TextContent.Contains("Add Setting", StringComparison.Ordinal)).Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, settings.Count);
            Assert.Contains("NEW_SETTING_2", cut.Find(".setting-key-large").TextContent);
            Assert.Equal("General", settings[1].Metadata.Category);
        });
    }

    [Fact]
    public void GameTypeRevisionSettingsEditor_AddSetting_ShouldReuseSelectedCategory()
    {
        var settings = new List<GameTypeRevisionSettingDraft>
        {
            new()
            {
                SettingKey = "EULA",
                DefaultValue = "TRUE",
                Metadata = new GameTypeRevisionSettingMetadataDraft { Category = "General", DataType = "boolean" }
            },
            new()
            {
                SettingKey = "RCON_PORT",
                DefaultValue = "25575",
                Metadata = new GameTypeRevisionSettingMetadataDraft { Category = "Network", DataType = "number" }
            }
        };

        var cut = Render<GameTypeRevisionSettingsEditor>(parameters => parameters
            .Add(p => p.Settings, settings)
            .Add(p => p.DefinedPorts, new List<GameTypeRevisionPortDraft>
            {
                new() { ContainerPort = 25565, Protocol = "tcp", AdvertisedPort = true }
            })
            .Add(p => p.DataTypeOptions, new[] { "string", "number", "boolean", "yesno", "enum", "port" })
            .Add(p => p.ProtocolOptions, new[] { "tcp", "udp" })
            .Add(p => p.PortMappingRoleOptions, new[] { "Primary", "Related" })
            .Add(p => p.PortRelationTypeOptions, new[] { "Direct", "Offset", "Fixed", "Multiplier" }));

        cut.FindAll(".setting-list-item")[1].Click();
        cut.FindAll("button").First(button => button.TextContent.Contains("Add Setting", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(3, settings.Count);
            Assert.Equal("Network", settings[2].Metadata.Category);
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
            .Add(p => p.DataTypeOptions, new[] { "string", "number", "boolean", "yesno", "enum", "port" })
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

    [Fact]
    public void GameTypeRevisionSettingsEditor_ShouldRenderYesNoDataTypeOption()
    {
        var settings = new List<GameTypeRevisionSettingDraft>
        {
            new()
            {
                SettingKey = "ENABLE_FEATURE",
                DefaultValue = "yes",
                Metadata = new GameTypeRevisionSettingMetadataDraft { Category = "General", DataType = "yesno" }
            }
        };

        var cut = Render<GameTypeRevisionSettingsEditor>(parameters => parameters
            .Add(p => p.Settings, settings)
            .Add(p => p.DefinedPorts, Array.Empty<GameTypeRevisionPortDraft>())
            .Add(p => p.DataTypeOptions, new[] { "string", "number", "boolean", "yesno", "enum", "port" })
            .Add(p => p.ProtocolOptions, new[] { "tcp", "udp" })
            .Add(p => p.PortMappingRoleOptions, new[] { "Primary", "Related" })
            .Add(p => p.PortRelationTypeOptions, new[] { "Direct", "Offset", "Fixed", "Multiplier" }));

        cut.Find(".setting-list-item").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("yes/no", cut.Markup);
            Assert.DoesNotContain(">yesno<", cut.Markup);
        });
    }

    [Fact]
    public void GameTypeRevisionSettingsEditor_ShouldRenderPasswordDataTypeOption()
    {
        var settings = new List<GameTypeRevisionSettingDraft>
        {
            new()
            {
                SettingKey = "SERVER_PASSWORD",
                DefaultValue = "secret123",
                Metadata = new GameTypeRevisionSettingMetadataDraft { Category = "Security", DataType = "password" }
            }
        };

        var cut = Render<GameTypeRevisionSettingsEditor>(parameters => parameters
            .Add(p => p.Settings, settings)
            .Add(p => p.DefinedPorts, Array.Empty<GameTypeRevisionPortDraft>())
            .Add(p => p.DataTypeOptions, new[] { "string", "password", "number", "boolean", "yesno", "enum", "port" })
            .Add(p => p.ProtocolOptions, new[] { "tcp", "udp" })
            .Add(p => p.PortMappingRoleOptions, new[] { "Primary", "Related" })
            .Add(p => p.PortRelationTypeOptions, new[] { "Direct", "Offset", "Fixed", "Multiplier" }));

        cut.Find(".setting-list-item").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Password", cut.Markup);
        });
    }

    [Fact]
    public void GameTypeRevisionSettingsEditor_MoveSetting_ShouldReorderWithinCategory()
    {
        var settings = new List<GameTypeRevisionSettingDraft>
        {
            new() { SettingKey = "ITEM_1", Metadata = new GameTypeRevisionSettingMetadataDraft { Category = "General" } },
            new() { SettingKey = "ITEM_2", Metadata = new GameTypeRevisionSettingMetadataDraft { Category = "General" } },
            new() { SettingKey = "ITEM_3", Metadata = new GameTypeRevisionSettingMetadataDraft { Category = "General" } }
        };

        var cut = Render<GameTypeRevisionSettingsEditor>(parameters => parameters
            .Add(p => p.Settings, settings)
            .Add(p => p.DefinedPorts, Array.Empty<GameTypeRevisionPortDraft>())
            .Add(p => p.DataTypeOptions, new[] { "string", "number" })
            .Add(p => p.ProtocolOptions, new[] { "tcp" }));

        // Select second item
        cut.FindAll(".setting-list-item")[1].Click();

        // Move up
        var moveUpBtn = cut.FindComponents<Radzen.Blazor.RadzenButton>().First(b => b.Instance.Icon == "arrow_upward");
        moveUpBtn.Find("button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("ITEM_2", settings[0].SettingKey);
            Assert.Equal("ITEM_1", settings[1].SettingKey);
        });

        // Move down
        var moveDownBtn = cut.FindComponents<Radzen.Blazor.RadzenButton>().First(b => b.Instance.Icon == "arrow_downward");
        moveDownBtn.Find("button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("ITEM_1", settings[0].SettingKey);
            Assert.Equal("ITEM_2", settings[1].SettingKey);
        });
    }

    [Fact]
    public void GameTypeRevisionSettingsEditor_DeleteSetting_ShouldRemoveAndSelectNext()
    {
        var settings = new List<GameTypeRevisionSettingDraft>
        {
            new() { SettingKey = "ITEM_1", Metadata = new GameTypeRevisionSettingMetadataDraft { Category = "General" } },
            new() { SettingKey = "ITEM_2", Metadata = new GameTypeRevisionSettingMetadataDraft { Category = "General" } }
        };

        var cut = Render<GameTypeRevisionSettingsEditor>(parameters => parameters
            .Add(p => p.Settings, settings)
            .Add(p => p.DefinedPorts, Array.Empty<GameTypeRevisionPortDraft>())
            .Add(p => p.DataTypeOptions, new[] { "string" })
            .Add(p => p.ProtocolOptions, new[] { "tcp" }));

        var deleteBtn = cut.FindComponents<Radzen.Blazor.RadzenButton>().First(b => b.Instance.Icon == "delete");
        deleteBtn.Find("button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Single(settings);
            Assert.Equal("ITEM_2", settings[0].SettingKey);
        });
    }

    [Fact]
    public void GameTypeRevisionSettingsEditor_SearchFilter_FiltersSettingsList()
    {
        var settings = new List<GameTypeRevisionSettingDraft>
        {
            new() { SettingKey = "ALPHA", Description = "First", Metadata = new GameTypeRevisionSettingMetadataDraft { Category = "CatA" } },
            new() { SettingKey = "BETA", Description = "Second", Metadata = new GameTypeRevisionSettingMetadataDraft { Category = "CatB" } }
        };

        var cut = Render<GameTypeRevisionSettingsEditor>(parameters => parameters
            .Add(p => p.Settings, settings)
            .Add(p => p.DefinedPorts, Array.Empty<GameTypeRevisionPortDraft>())
            .Add(p => p.DataTypeOptions, new[] { "string" }));

        var searchBox = cut.Find("input[placeholder='Search by key...']");
        searchBox.Input("BETA");

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("ALPHA", cut.Find(".settings-list-panel").TextContent);
            Assert.Contains("BETA", cut.Find(".settings-list-panel").TextContent);
        });
    }

    [Fact]
    public void GameTypeRevisionSettingsEditor_EnumDataType_AddAndRemoveValues()
    {
        var setting = new GameTypeRevisionSettingDraft
        {
            SettingKey = "DIFFICULTY",
            Metadata = new GameTypeRevisionSettingMetadataDraft
            {
                DataType = "enum",
                AllowedValuesJson = "[\"easy\",\"normal\",\"hard\"]",
                ValueMappingsJson = "{\"easy\":\"Easy Mode\",\"normal\":\"Normal Mode\",\"hard\":\"Hard Mode\"}"
            }
        };

        var cut = Render<GameTypeRevisionSettingsEditor>(parameters => parameters
            .Add(p => p.Settings, new List<GameTypeRevisionSettingDraft> { setting })
            .Add(p => p.DefinedPorts, Array.Empty<GameTypeRevisionPortDraft>())
            .Add(p => p.DataTypeOptions, new[] { "string", "enum" }));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(3, setting.Metadata.EnumValues.Count);
            Assert.Contains("Enum Configuration", cut.Markup);
        });

        // Click Add Value
        var addValBtn = cut.FindComponents<Radzen.Blazor.RadzenButton>().First(b => b.Instance.Text == "Add Value");
        addValBtn.Find("button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(4, setting.Metadata.EnumValues.Count);
        });

        // Click Remove Value
        var removeValBtn = cut.FindComponents<Radzen.Blazor.RadzenButton>().First(b => b.Instance.Icon == "close");
        removeValBtn.Find("button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(3, setting.Metadata.EnumValues.Count);
        });
    }

    [Fact]
    public void GameTypeRevisionSettingsEditor_ServerVariable_RendersAndEditsTokens()
    {
        var setting = new GameTypeRevisionSettingDraft
        {
            SettingKey = "SERVER_NAME",
            DefaultValue = "srv_{SERVER_ID}",
            Metadata = new GameTypeRevisionSettingMetadataDraft
            {
                DataType = "servervariable"
            }
        };

        var cut = Render<GameTypeRevisionSettingsEditor>(parameters => parameters
            .Add(p => p.Settings, new List<GameTypeRevisionSettingDraft> { setting })
            .Add(p => p.DefinedPorts, Array.Empty<GameTypeRevisionPortDraft>())
            .Add(p => p.DataTypeOptions, new[] { "string", "servervariable" }));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Default Control:", cut.Markup);
            Assert.Contains("Tokens:", cut.Markup);
        });
    }

    [Fact]
    public void GameTypeRevisionSettingsEditor_PortMappings_AddAndConfigure()
    {
        var ports = new List<GameTypeRevisionPortDraft>
        {
            new() { ContainerPort = 7777, Protocol = "udp", Description = "Game Port" },
            new() { ContainerPort = 7778, Protocol = "udp", Description = "Query Port" }
        };

        var setting = new GameTypeRevisionSettingDraft
        {
            SettingKey = "GAME_PORT",
            DefaultValue = "7777",
            Metadata = new GameTypeRevisionSettingMetadataDraft
            {
                DataType = "port",
                PortMappings = []
            }
        };

        var cut = Render<GameTypeRevisionSettingsEditor>(parameters => parameters
            .Add(p => p.Settings, new List<GameTypeRevisionSettingDraft> { setting })
            .Add(p => p.DefinedPorts, ports)
            .Add(p => p.DataTypeOptions, new[] { "port", "string" })
            .Add(p => p.ProtocolOptions, new[] { "udp", "tcp" })
            .Add(p => p.PortMappingRoleOptions, new[] { "Primary", "Related" })
            .Add(p => p.PortRelationTypeOptions, new[] { "Direct", "Offset", "Multiplier" }));

        // Add primary port mapping
        var addMappingBtn = cut.FindComponents<Radzen.Blazor.RadzenButton>().First(b => b.Instance.Text == "Add Port Mapping");
        addMappingBtn.Find("button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Single(setting.Metadata.PortMappings);
            Assert.Equal("Primary", setting.Metadata.PortMappings[0].MappingRole);
        });

        // Add related port mapping
        addMappingBtn.Find("button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, setting.Metadata.PortMappings.Count);
            Assert.Equal("Related", setting.Metadata.PortMappings[1].MappingRole);
        });

        // Remove mapping
        var deleteMappingBtn = cut.FindComponents<Radzen.Blazor.RadzenButton>().First(b => b.Instance.Icon == "delete" && b.Instance.Text != "Delete");
        deleteMappingBtn.Find("button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Single(setting.Metadata.PortMappings);
        });
    }
}
