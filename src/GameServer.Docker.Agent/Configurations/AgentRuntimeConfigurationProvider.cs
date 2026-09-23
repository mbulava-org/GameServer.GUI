using Microsoft.Extensions.Configuration;

namespace GameServer.Docker.Agent.Configurations
{
    public sealed class AgentRuntimeConfigurationSource : IConfigurationSource
    {
        public AgentRuntimeConfigurationProvider Provider { get; } = new();

        public IConfigurationProvider Build(IConfigurationBuilder builder) => Provider;
    }

    public sealed class AgentRuntimeConfigurationProvider : ConfigurationProvider
    {
        private readonly object _sync = new();

        public void Replace(IEnumerable<KeyValuePair<string, string?>> values)
        {
            lock (_sync)
            {
                Data = values
                    .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            }

            OnReload();
        }
    }
}
