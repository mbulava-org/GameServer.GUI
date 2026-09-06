using GameServer.API.Interfaces;
using GameServer.API.Models;
using Microsoft.AspNetCore.SignalR;

namespace GameServer.Orchestration.Host.Hubs;

/// <summary>
/// SignalR hub for agent registration and heartbeats.
/// Agents (GameServer.Docker.Agent) connect here and push their state.
/// Moved from GameServer.API in Phase 2 so the Orchestration service
/// owns the agent lifecycle.
/// </summary>
public class AgentRegistrationHub(
    IAgentRegistry agentRegistry,
    ILogger<AgentRegistrationHub> logger) : Hub
{
    /// <summary>Called by agents on initial connection to register themselves.</summary>
    public async Task RegisterAgent(AgentRegistrationInfo info)
    {
        var connectionId = Context.ConnectionId;
        logger.LogInformation(
            "Agent registration: Node={NodeName} ({NodeId}), Connection={ConnectionId}, Url={Url}",
            info.NodeName, info.NodeId, connectionId, info.InternalUrl);
        agentRegistry.RegisterAgent(info, connectionId);
        await Task.CompletedTask;
    }

    /// <summary>Called periodically by agents to report container list and health.</summary>
    public async Task SendHeartbeat(AgentHeartbeatInfo heartbeat)
    {
        logger.LogTrace(
            "Heartbeat: Node={NodeId}, Connection={ConnectionId}, Containers={Count}, Health={Health}",
            heartbeat.NodeId, Context.ConnectionId, heartbeat.ContainerIds.Count, heartbeat.Health);
        agentRegistry.UpdateAgentContainers(Context.ConnectionId, heartbeat.ContainerIds);
        await Task.CompletedTask;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
            logger.LogWarning(exception, "Agent disconnected with error: Connection={ConnectionId}", Context.ConnectionId);
        else
            logger.LogInformation("Agent disconnected: Connection={ConnectionId}", Context.ConnectionId);

        agentRegistry.MarkAgentDisconnected(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public override async Task OnConnectedAsync()
    {
        logger.LogInformation("Agent connected: Connection={ConnectionId}, IP={IP}",
            Context.ConnectionId,
            Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        await base.OnConnectedAsync();
    }
}
