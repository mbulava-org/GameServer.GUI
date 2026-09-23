using GameServer.Docker.Agent.Configurations;
using GameServer.Docker.Agent.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace GameServer.Docker.Agent.Tests.Services;

public class AgentDistributedConfigurationApplierTests
{
    [Fact]
    public void Apply_ReplacesExistingValuesAndFiltersBootstrapPrimaryServiceUrl()
    {
        var provider = new AgentRuntimeConfigurationProvider();
        provider.Replace(new Dictionary<string, string?>
        {
            ["AgentRegistration:HeartbeatIntervalSeconds"] = "30",
            ["Legacy:Setting"] = "keep-me-out"
        });

        var applier = new AgentDistributedConfigurationApplier(
            provider,
            new Mock<ILogger<AgentDistributedConfigurationApplier>>().Object);

        applier.Apply(new Dictionary<string, string?>
        {
            ["AgentRegistration:HeartbeatIntervalSeconds"] = "15",
            ["AgentRegistration:PrimaryServiceUrl"] = "http://other-service:8080",
            ["ContainerStats:SampleWindowSeconds"] = "5"
        });

        Assert.True(provider.TryGet("AgentRegistration:HeartbeatIntervalSeconds", out var heartbeatInterval));
        Assert.Equal("15", heartbeatInterval);

        Assert.True(provider.TryGet("ContainerStats:SampleWindowSeconds", out var sampleWindow));
        Assert.Equal("5", sampleWindow);

        Assert.False(provider.TryGet("Legacy:Setting", out _));
        Assert.False(provider.TryGet("AgentRegistration:PrimaryServiceUrl", out _));
    }
}
