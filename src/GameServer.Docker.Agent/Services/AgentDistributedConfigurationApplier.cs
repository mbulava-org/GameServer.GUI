using GameServer.Docker.Agent.Configurations;

namespace GameServer.Docker.Agent.Services
{
    public sealed class AgentDistributedConfigurationApplier(
        AgentRuntimeConfigurationProvider provider,
        ILogger<AgentDistributedConfigurationApplier> logger)
    {
        private static readonly HashSet<string> DisallowedKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "AgentRegistration:PrimaryServiceUrl"
        };

        public void Apply(IReadOnlyDictionary<string, string?> settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var sanitized = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in settings)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                if (DisallowedKeys.Contains(pair.Key))
                {
                    logger.LogWarning(
                        "Ignoring distributed configuration key '{Key}' because it is a local bootstrap setting.",
                        pair.Key);
                    continue;
                }

                sanitized[pair.Key] = pair.Value;
            }

            provider.Replace(sanitized);

            logger.LogInformation(
                "Applied {Count} distributed agent configuration value(s) from the Primary Service.",
                sanitized.Count);
        }
    }
}
