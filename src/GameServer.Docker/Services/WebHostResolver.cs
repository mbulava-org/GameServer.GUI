using GameServer.Docker.Models;
using Microsoft.Extensions.Logging;

namespace GameServer.Docker.Services;

/// <summary>
/// Resolves web host definitions by evaluating conditions and dynamic port values
/// against a server's actual settings.
/// </summary>
[Obsolete("This resolver still depends on legacy web host definitions. Migrate to V2 web host models before removing the old repository chain.")]
public class WebHostResolver(ILogger<WebHostResolver> logger)
{
    /// <summary>
    /// Resolves all web hosts for a server, filtering by EnabledWhen conditions
    /// and resolving dynamic ports.
    /// </summary>
    public List<ResolvedWebHost> ResolveWebHosts(
        List<WebHostDefinition> definitions,
        Dictionary<string, string> serverSettings)
    {
        var resolved = new List<ResolvedWebHost>();

        foreach (var definition in definitions)
        {
            // Check if host is enabled based on condition
            if (!IsHostEnabled(definition, serverSettings))
            {
                logger.LogDebug("Web host '{Name}' is disabled by condition: {Condition}",
                    definition.Name, definition.EnabledWhen);
                continue;
            }

            // Resolve the container port
            var port = ResolveContainerPort(definition, serverSettings);
            if (!port.HasValue)
            {
                logger.LogWarning("Web host '{Name}' could not resolve port from variable '{Variable}'",
                    definition.Name, definition.ContainerPortVariable);
                continue;
            }

            resolved.Add(new ResolvedWebHost
            {
                Name = definition.Name,
                ContainerPort = port.Value,
                Description = definition.Description,
                PathSegment = definition.PathSegment ?? definition.Name.ToLower().Replace(" ", "-"),
                RequiresAuth = definition.RequiresAuth
            });
        }

        return resolved;
    }

    /// <summary>
    /// Checks if a web host should be enabled based on its EnabledWhen condition.
    /// </summary>
    private bool IsHostEnabled(WebHostDefinition definition, Dictionary<string, string> serverSettings)
    {
        if (string.IsNullOrWhiteSpace(definition.EnabledWhen))
        {
            return true; // No condition = always enabled
        }

        var condition = definition.EnabledWhen.Trim();

        // Parse condition: "VAR=value" or "VAR!=value"
        string varName, expectedValue;
        bool isNegated = false;

        if (condition.Contains("!="))
        {
            var parts = condition.Split("!=", 2);
            if (parts.Length != 2) return false;
            varName = parts[0].Trim();
            expectedValue = parts[1].Trim();
            isNegated = true;
        }
        else if (condition.Contains("="))
        {
            var parts = condition.Split("=", 2);
            if (parts.Length != 2) return false;
            varName = parts[0].Trim();
            expectedValue = parts[1].Trim();
        }
        else
        {
            logger.LogWarning("Invalid EnabledWhen condition format: {Condition}", condition);
            return false;
        }

        // Get actual value from server settings
        if (!serverSettings.TryGetValue(varName, out var actualValue))
        {
            // Variable not set = treat as empty string
            actualValue = "";
        }

        var matches = string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase);
        return isNegated ? !matches : matches;
    }

    /// <summary>
    /// Resolves the container port from either the fixed value or a dynamic variable.
    /// </summary>
    private int? ResolveContainerPort(WebHostDefinition definition, Dictionary<string, string> serverSettings)
    {
        // If no variable specified, use fixed port
        if (string.IsNullOrWhiteSpace(definition.ContainerPortVariable))
        {
            return definition.ContainerPort;
        }

        // Try to get port from variable
        if (!serverSettings.TryGetValue(definition.ContainerPortVariable, out var portString))
        {
            logger.LogWarning("Port variable '{Variable}' not found in server settings",
                definition.ContainerPortVariable);
            return null;
        }

        if (!int.TryParse(portString, out var port) || port <= 0 || port > 65535)
        {
            logger.LogWarning("Port variable '{Variable}' has invalid value: {Value}",
                definition.ContainerPortVariable, portString);
            return null;
        }

        return port;
    }
}

/// <summary>
/// A web host with all conditions evaluated and ports resolved.
/// </summary>
public class ResolvedWebHost
{
    public string Name { get; set; } = "";
    public int ContainerPort { get; set; }
    public string Description { get; set; } = "";
    public string PathSegment { get; set; } = "";
    public bool RequiresAuth { get; set; }
}
