using GameServer.API.Interfaces;
using GameServer.API.Services;
using Microsoft.Extensions.Configuration;

namespace GameServer.API.Tests.Services;

public class AgentDistributedConfigurationServiceTests
{
    [Fact]
    public void GetConfigurationSnapshot_FlattensNestedDistributedAgentConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DistributedAgentConfiguration:AgentRegistration:HeartbeatIntervalSeconds"] = "15",
                ["DistributedAgentConfiguration:AgentRegistration:Capabilities:0"] = "logs",
                ["DistributedAgentConfiguration:AgentRegistration:Capabilities:1"] = "stats",
                ["DistributedAgentConfiguration:ContainerStats:SampleWindowSeconds"] = "5"
            })
            .Build();

        IAgentDistributedConfigurationService service = new AgentDistributedConfigurationService(configuration);

        var snapshot = service.GetConfigurationSnapshot();

        Assert.Equal("15", snapshot["AgentRegistration:HeartbeatIntervalSeconds"]);
        Assert.Equal("logs", snapshot["AgentRegistration:Capabilities:0"]);
        Assert.Equal("stats", snapshot["AgentRegistration:Capabilities:1"]);
        Assert.Equal("5", snapshot["ContainerStats:SampleWindowSeconds"]);
    }

    [Fact]
    public void GetConfigurationSnapshot_ReturnsEmptyWhenSectionMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        IAgentDistributedConfigurationService service = new AgentDistributedConfigurationService(configuration);

        var snapshot = service.GetConfigurationSnapshot();

        Assert.Empty(snapshot);
    }
}
