using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using GameServer.API.Interfaces;
using GameServer.API.Models;

namespace GameServer.Integration.Tests.Hubs;

[Collection("Integration Tests")]
public class SignalRHubsIntegrationTests
{
    private readonly IntegrationTestFactory _factory;

    public SignalRHubsIntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    private HubConnection CreateHubClient(string hubPath)
    {
        var server = _factory.Server;
        var url = server.BaseAddress!.AbsoluteUri.TrimEnd('/') + hubPath;

        return new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            })
            .WithAutomaticReconnect()
            .Build();
    }

    [Fact]
    public async Task ServerLogsHub_ConnectsSuccessfully()
    {
        var client = CreateHubClient("/hubs/serverlogs");
        await client.StartAsync();

        Assert.Equal(HubConnectionState.Connected, client.State);

        await client.StopAsync();
    }

    [Fact]
    public async Task ResourceMonitoringHub_ConnectsAndHandlesUnsubscribe()
    {
        var client = CreateHubClient("/hubs/resources");
        await client.StartAsync();

        Assert.Equal(HubConnectionState.Connected, client.State);

        // Unsubscribe invocation
        await client.InvokeAsync("Unsubscribe");
        await client.StopAsync();
    }

    [Fact]
    public async Task AgentRegistrationHub_RegisterAgent_ReturnsSuccess()
    {
        var client = CreateHubClient("/hubs/agentregistration");
        await client.StartAsync();

        Assert.Equal(HubConnectionState.Connected, client.State);

        var registration = new AgentRegistrationInfo
        {
            NodeId = "test-node-1",
            NodeName = "node-1",
            InternalUrl = "http://node-1:5000",
            Capabilities = ["docker", "exec"]
        };

        await client.InvokeAsync("RegisterAgent", registration);

        var heartbeat = new AgentHeartbeatInfo
        {
            NodeId = "test-node-1",
            Timestamp = DateTime.UtcNow,
            Health = "healthy"
        };

        await client.InvokeAsync("SendHeartbeat", heartbeat);

        await client.StopAsync();
    }
}
