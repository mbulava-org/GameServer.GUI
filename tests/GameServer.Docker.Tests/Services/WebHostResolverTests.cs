using GameServer.Docker.Models;
using GameServer.Docker.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameServer.Docker.Tests.Services;

public class WebHostResolverTests
{
    private readonly Mock<ILogger<WebHostResolver>> _mockLogger;
    private readonly WebHostResolver _resolver;

    public WebHostResolverTests()
    {
        _mockLogger = new Mock<ILogger<WebHostResolver>>();
        _resolver = new WebHostResolver(_mockLogger.Object);
    }

    #region Condition Evaluation Tests

    [Fact]
    public void ResolveWebHosts_NoCondition_ShouldEnableHost()
    {
        // Arrange
        var hosts = new List<WebHostDefinition>
        {
            new()
            {
                Name = "Dynmap",
                ContainerPort = 8123,
                EnabledWhen = null // No condition
            }
        };

        var settings = new Dictionary<string, string>();

        // Act
        var result = _resolver.ResolveWebHosts(hosts, settings);

        // Assert
        Assert.Single(result);
        Assert.Equal("Dynmap", result[0].Name);
        Assert.Equal(8123, result[0].ContainerPort);
    }

    [Fact]
    public void ResolveWebHosts_EqualityConditionMet_ShouldEnableHost()
    {
        // Arrange
        var hosts = new List<WebHostDefinition>
        {
            new()
            {
                Name = "Dynmap",
                ContainerPort = 8123,
                EnabledWhen = "DYNMAP_ENABLED=true"
            }
        };

        var settings = new Dictionary<string, string>
        {
            ["DYNMAP_ENABLED"] = "true"
        };

        // Act
        var result = _resolver.ResolveWebHosts(hosts, settings);

        // Assert
        Assert.Single(result);
        Assert.Equal("Dynmap", result[0].Name);
    }

    [Fact]
    public void ResolveWebHosts_EqualityConditionNotMet_ShouldDisableHost()
    {
        // Arrange
        var hosts = new List<WebHostDefinition>
        {
            new()
            {
                Name = "Dynmap",
                ContainerPort = 8123,
                EnabledWhen = "DYNMAP_ENABLED=true"
            }
        };

        var settings = new Dictionary<string, string>
        {
            ["DYNMAP_ENABLED"] = "false"
        };

        // Act
        var result = _resolver.ResolveWebHosts(hosts, settings);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ResolveWebHosts_VariableMissing_ShouldDisableHost()
    {
        // Arrange
        var hosts = new List<WebHostDefinition>
        {
            new()
            {
                Name = "Dynmap",
                ContainerPort = 8123,
                EnabledWhen = "DYNMAP_ENABLED=true"
            }
        };

        var settings = new Dictionary<string, string>(); // Variable not set

        // Act
        var result = _resolver.ResolveWebHosts(hosts, settings);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ResolveWebHosts_InequalityConditionMet_ShouldEnableHost()
    {
        // Arrange
        var hosts = new List<WebHostDefinition>
        {
            new()
            {
                Name = "Admin",
                ContainerPort = 8080,
                EnabledWhen = "MODE!=disabled"
            }
        };

        var settings = new Dictionary<string, string>
        {
            ["MODE"] = "production"
        };

        // Act
        var result = _resolver.ResolveWebHosts(hosts, settings);

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public void ResolveWebHosts_InequalityConditionNotMet_ShouldDisableHost()
    {
        // Arrange
        var hosts = new List<WebHostDefinition>
        {
            new()
            {
                Name = "Admin",
                ContainerPort = 8080,
                EnabledWhen = "MODE!=disabled"
            }
        };

        var settings = new Dictionary<string, string>
        {
            ["MODE"] = "disabled"
        };

        // Act
        var result = _resolver.ResolveWebHosts(hosts, settings);

        // Assert
        Assert.Empty(result);
    }

    [Theory]
    [InlineData("ENABLED=true", "true", true)]
    [InlineData("ENABLED=true", "TRUE", true)]
    [InlineData("ENABLED=true", "True", true)]
    [InlineData("ENABLED=false", "false", true)]
    [InlineData("ENABLED=false", "true", false)]
    public void ResolveWebHosts_CaseInsensitiveComparison(string condition, string value, bool shouldEnable)
    {
        // Arrange
        var hosts = new List<WebHostDefinition>
        {
            new()
            {
                Name = "Test",
                ContainerPort = 8080,
                EnabledWhen = condition
            }
        };

        var settings = new Dictionary<string, string>
        {
            ["ENABLED"] = value
        };

        // Act
        var result = _resolver.ResolveWebHosts(hosts, settings);

        // Assert
        if (shouldEnable)
        {
            Assert.Single(result);
        }
        else
        {
            Assert.Empty(result);
        }
    }

    #endregion

    #region Port Resolution Tests

    [Fact]
    public void ResolveWebHosts_FixedPort_ShouldUseConfiguredPort()
    {
        // Arrange
        var hosts = new List<WebHostDefinition>
        {
            new()
            {
                Name = "Dynmap",
                ContainerPort = 8123,
                ContainerPortVariable = null
            }
        };

        var settings = new Dictionary<string, string>();

        // Act
        var result = _resolver.ResolveWebHosts(hosts, settings);

        // Assert
        Assert.Single(result);
        Assert.Equal(8123, result[0].ContainerPort);
    }

    [Fact]
    public void ResolveWebHosts_DynamicPortSet_ShouldUseVariablePort()
    {
        // Arrange
        var hosts = new List<WebHostDefinition>
        {
            new()
            {
                Name = "Admin",
                ContainerPort = 8080, // Default/fallback
                ContainerPortVariable = "WEBUI_PORT"
            }
        };

        var settings = new Dictionary<string, string>
        {
            ["WEBUI_PORT"] = "9090"
        };

        // Act
        var result = _resolver.ResolveWebHosts(hosts, settings);

        // Assert
        Assert.Single(result);
        Assert.Equal(9090, result[0].ContainerPort);
    }

    [Fact]
    public void ResolveWebHosts_DynamicPortNotSet_ShouldDisableHost()
    {
        // Arrange
        var hosts = new List<WebHostDefinition>
        {
            new()
            {
                Name = "Admin",
                ContainerPort = 8080,
                ContainerPortVariable = "WEBUI_PORT"
            }
        };

        var settings = new Dictionary<string, string>(); // Port variable not set

        // Act
        var result = _resolver.ResolveWebHosts(hosts, settings);

        // Assert
        Assert.Empty(result); // Host disabled because port variable missing
    }

    [Theory]
    [InlineData("0", false)]      // Zero port
    [InlineData("-1", false)]     // Negative
    [InlineData("65536", false)]  // Above max
    [InlineData("100000", false)] // Way above max
    [InlineData("abc", false)]    // Non-numeric
    [InlineData("", false)]       // Empty
    [InlineData("8080", true)]    // Valid
    [InlineData("1", true)]       // Min valid
    [InlineData("65535", true)]   // Max valid
    public void ResolveWebHosts_DynamicPortValidation(string portValue, bool shouldEnable)
    {
        // Arrange
        var hosts = new List<WebHostDefinition>
        {
            new()
            {
                Name = "Test",
                ContainerPort = 8080,
                ContainerPortVariable = "TEST_PORT"
            }
        };

        var settings = new Dictionary<string, string>
        {
            ["TEST_PORT"] = portValue
        };

        // Act
        var result = _resolver.ResolveWebHosts(hosts, settings);

        // Assert
        if (shouldEnable)
        {
            Assert.Single(result);
            Assert.Equal(int.Parse(portValue), result[0].ContainerPort);
        }
        else
        {
            Assert.Empty(result);
        }
    }

    #endregion

    #region Path Segment Tests

    [Fact]
    public void ResolveWebHosts_CustomPathSegment_ShouldUseCustom()
    {
        // Arrange
        var hosts = new List<WebHostDefinition>
        {
            new()
            {
                Name = "Dynmap",
                ContainerPort = 8123,
                PathSegment = "custom-map"
            }
        };

        var settings = new Dictionary<string, string>();

        // Act
        var result = _resolver.ResolveWebHosts(hosts, settings);

        // Assert
        Assert.Single(result);
        Assert.Equal("custom-map", result[0].PathSegment);
    }

    [Fact]
    public void ResolveWebHosts_NoPathSegment_ShouldGenerateFromName()
    {
        // Arrange
        var hosts = new List<WebHostDefinition>
        {
            new()
            {
                Name = "Admin Panel",
                ContainerPort = 8080,
                PathSegment = null
            }
        };

        var settings = new Dictionary<string, string>();

        // Act
        var result = _resolver.ResolveWebHosts(hosts, settings);

        // Assert
        Assert.Single(result);
        Assert.Equal("admin-panel", result[0].PathSegment);
    }

    [Theory]
    [InlineData("Simple", "simple")]
    [InlineData("Admin Panel", "admin-panel")]
    [InlineData("Web UI", "web-ui")]
    [InlineData("Multiple   Spaces", "multiple---spaces")] // Multiple spaces become multiple dashes
    [InlineData("UPPERCASE", "uppercase")]
    public void ResolveWebHosts_PathSegmentGeneration(string name, string expectedSegment)
    {
        // Arrange
        var hosts = new List<WebHostDefinition>
        {
            new()
            {
                Name = name,
                ContainerPort = 8080,
                PathSegment = null
            }
        };

        var settings = new Dictionary<string, string>();

        // Act
        var result = _resolver.ResolveWebHosts(hosts, settings);

        // Assert
        Assert.Single(result);
        Assert.Equal(expectedSegment, result[0].PathSegment);
    }

    #endregion

    #region Multiple Hosts Tests

    [Fact]
    public void ResolveWebHosts_MultipleHosts_AllEnabled_ShouldReturnAll()
    {
        // Arrange
        var hosts = new List<WebHostDefinition>
        {
            new()
            {
                Name = "Dynmap",
                ContainerPort = 8123
            },
            new()
            {
                Name = "Admin",
                ContainerPort = 8080
            },
            new()
            {
                Name = "Metrics",
                ContainerPort = 9090
            }
        };

        var settings = new Dictionary<string, string>();

        // Act
        var result = _resolver.ResolveWebHosts(hosts, settings);

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void ResolveWebHosts_MultipleHosts_SomeDisabled_ShouldReturnOnlyEnabled()
    {
        // Arrange
        var hosts = new List<WebHostDefinition>
        {
            new()
            {
                Name = "Dynmap",
                ContainerPort = 8123,
                EnabledWhen = "DYNMAP_ENABLED=true"
            },
            new()
            {
                Name = "BlueMap",
                ContainerPort = 8100,
                EnabledWhen = "BLUEMAP_ENABLED=true"
            },
            new()
            {
                Name = "Admin",
                ContainerPort = 8080
            }
        };

        var settings = new Dictionary<string, string>
        {
            ["DYNMAP_ENABLED"] = "true",
            ["BLUEMAP_ENABLED"] = "false"
        };

        // Act
        var result = _resolver.ResolveWebHosts(hosts, settings);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, h => h.Name == "Dynmap");
        Assert.Contains(result, h => h.Name == "Admin");
        Assert.DoesNotContain(result, h => h.Name == "BlueMap");
    }

    [Fact]
    public void ResolveWebHosts_ComplexScenario_MixedConditionsAndPorts()
    {
        // Arrange
        var hosts = new List<WebHostDefinition>
        {
            new()
            {
                Name = "Dynmap",
                ContainerPort = 8123,
                EnabledWhen = "DYNMAP_ENABLED=true"
            },
            new()
            {
                Name = "Admin",
                ContainerPort = 8080,
                ContainerPortVariable = "ADMIN_PORT",
                EnabledWhen = "ADMIN_ENABLED=true"
            },
            new()
            {
                Name = "Metrics",
                ContainerPort = 9090
            }
        };

        var settings = new Dictionary<string, string>
        {
            ["DYNMAP_ENABLED"] = "true",
            ["ADMIN_ENABLED"] = "true",
            ["ADMIN_PORT"] = "8888"
        };

        // Act
        var result = _resolver.ResolveWebHosts(hosts, settings);

        // Assert
        Assert.Equal(3, result.Count);

        var dynmap = result.First(h => h.Name == "Dynmap");
        Assert.Equal(8123, dynmap.ContainerPort);

        var admin = result.First(h => h.Name == "Admin");
        Assert.Equal(8888, admin.ContainerPort); // Dynamic port

        var metrics = result.First(h => h.Name == "Metrics");
        Assert.Equal(9090, metrics.ContainerPort);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ResolveWebHosts_EmptyList_ShouldReturnEmpty()
    {
        // Arrange
        var hosts = new List<WebHostDefinition>();
        var settings = new Dictionary<string, string>();

        // Act
        var result = _resolver.ResolveWebHosts(hosts, settings);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ResolveWebHosts_NullSettings_ShouldHandleGracefully()
    {
        // Arrange
        var hosts = new List<WebHostDefinition>
        {
            new()
            {
                Name = "Test",
                ContainerPort = 8080
            }
        };

        // Act
        var result = _resolver.ResolveWebHosts(hosts, null!);

        // Assert
        Assert.Single(result); // Host with no condition should still work
    }

    [Fact]
    public void ResolveWebHosts_MalformedCondition_ShouldDisableHost()
    {
        // Arrange
        var hosts = new List<WebHostDefinition>
        {
            new()
            {
                Name = "Test",
                ContainerPort = 8080,
                EnabledWhen = "INVALID CONDITION FORMAT"
            }
        };

        var settings = new Dictionary<string, string>();

        // Act
        var result = _resolver.ResolveWebHosts(hosts, settings);

        // Assert
        Assert.Empty(result); // Malformed condition = disabled
    }

    [Fact]
    public void ResolveWebHosts_PreservesOtherProperties()
    {
        // Arrange
        var hosts = new List<WebHostDefinition>
        {
            new()
            {
                Name = "Admin Panel",
                ContainerPort = 8080,
                Description = "Web-based administration",
                PathSegment = "admin",
                RequiresAuth = true
            }
        };

        var settings = new Dictionary<string, string>();

        // Act
        var result = _resolver.ResolveWebHosts(hosts, settings);

        // Assert
        Assert.Single(result);
        Assert.Equal("Admin Panel", result[0].Name);
        Assert.Equal("Web-based administration", result[0].Description);
        Assert.Equal("admin", result[0].PathSegment);
        Assert.True(result[0].RequiresAuth);
    }

    #endregion
}
