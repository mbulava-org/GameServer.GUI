using GameServer.API.Interfaces;

namespace GameServer.API.Services;

public sealed class AgentDistributedConfigurationService(IConfiguration configuration) : IAgentDistributedConfigurationService
{
    public const string SectionName = "DistributedAgentConfiguration";

    private static readonly string[] AllowedExactKeys =
    [
        "AgentRegistration:HeartbeatIntervalSeconds",
        "AgentRegistration:ManagedContainerReconciliationIntervalSeconds"
    ];

    private static readonly string[] AllowedPrefixes =
    [
        "AgentRegistration:Capabilities:",
        "ContainerStats:"
    ];

    public IReadOnlyDictionary<string, string?> GetConfigurationSnapshot()
    {
        var section = configuration.GetSection(SectionName);
        if (!section.Exists())
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }

        var flattened = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        Flatten(section, prefix: string.Empty, flattened);

        return flattened
            .Where(kvp => IsAllowedDistributedSetting(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsAllowedDistributedSetting(string key)
    {
        return AllowedExactKeys.Contains(key, StringComparer.OrdinalIgnoreCase) ||
               AllowedPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static void Flatten(IConfigurationSection section, string prefix, IDictionary<string, string?> values)
    {
        var children = section.GetChildren().ToList();
        if (children.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(prefix))
            {
                values[prefix] = section.Value;
            }

            return;
        }

        foreach (var child in children)
        {
            var nextPrefix = string.IsNullOrEmpty(prefix) ? child.Key : $"{prefix}:{child.Key}";
            Flatten(child, nextPrefix, values);
        }
    }
}
