using GameServer.Docker.Interfaces;
using GameServer.Docker.Models;
using Microsoft.AspNetCore.SignalR;

namespace GameServer.Docker.Hubs
{
    /// <summary>
    /// SignalR hub for agent registration and heartbeats.
    /// Agents connect to this hub and push their state to the Primary Service.
    /// This eliminates the need for the Primary Service to query Docker Swarm.
    /// </summary>
    public class AgentRegistrationHub : Hub
    {
        private readonly IAgentRegistry _agentRegistry;
        private readonly ILogger<AgentRegistrationHub> _logger;

        public AgentRegistrationHub(
            IAgentRegistry agentRegistry,
            ILogger<AgentRegistrationHub> logger)
        {
            _agentRegistry = agentRegistry;
            _logger = logger;
        }

        /// <summary>
        /// Called by agents on initial connection to register themselves
        /// </summary>
        public async Task RegisterAgent(AgentRegistrationInfo info)
        {
            var connectionId = Context.ConnectionId;

            _logger.LogInformation(
                "Agent registration request: Node={NodeName} ({NodeId}), ConnectionId={ConnectionId}, Url={Url}",
                info.NodeName,
                info.NodeId,
                connectionId,
                info.InternalUrl);

            _agentRegistry.RegisterAgent(info, connectionId);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Called by agents periodically to report their container list and health
        /// </summary>
        public async Task SendHeartbeat(AgentHeartbeatInfo heartbeat)
        {
            var connectionId = Context.ConnectionId;

            _logger.LogTrace(
                "Agent heartbeat: Node={NodeId}, ConnectionId={ConnectionId}, Containers={ContainerCount}, Health={Health}",
                heartbeat.NodeId,
                connectionId,
                heartbeat.ContainerIds.Count,
                heartbeat.Health);

            _agentRegistry.UpdateAgentContainers(connectionId, heartbeat.ContainerIds);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Called automatically by SignalR when an agent disconnects
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;

            if (exception != null)
            {
                _logger.LogWarning(exception,
                    "Agent disconnected with exception: ConnectionId={ConnectionId}",
                    connectionId);
            }
            else
            {
                _logger.LogInformation(
                    "Agent disconnected gracefully: ConnectionId={ConnectionId}",
                    connectionId);
            }

            _agentRegistry.MarkAgentDisconnected(connectionId);

            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Called automatically by SignalR when an agent connects
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation(
                "Agent connected: ConnectionId={ConnectionId}, IP={IP}",
                Context.ConnectionId,
                Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString() ?? "unknown");

            await base.OnConnectedAsync();
        }
    }
}
