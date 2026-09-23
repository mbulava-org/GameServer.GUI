namespace GameServer.API.Interfaces;

public interface IAgentDistributedConfigurationService
{
    IReadOnlyDictionary<string, string?> GetConfigurationSnapshot();
}
