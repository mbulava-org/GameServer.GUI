using GameServer.API.Interfaces;

namespace GameServer.API.Services;

public sealed class AgentDistributedConfigurationService(IConfiguration configuration) : IAgentDistributedConfigurationService
{
    public const string SectionName = "DistributedAgentConfiguration";

    public IReadOnlyDictionary<string, string?> GetConfigurationSnapshot()
    {
        var section = configuration.GetSection(SectionName);
        if (!section.Exists())
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }

        var snapshot = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        Flatten(section, prefix: string.Empty, snapshot);
        return snapshot;
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
