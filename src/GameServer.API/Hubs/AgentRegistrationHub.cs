using GameServer.API.Interfaces;
using GameServer.API.Models;
using Microsoft.AspNetCore.SignalR;

namespace GameServer.API.Hubs
{
    /// <summary>
    /// SignalR hub for agent registration and heartbeats.
    /// Agents connect to this hub and push their state to the Primary Service.
    /// This eliminates the need for the Primary Service to query Docker Swarm.
    /// </summary>
    public class AgentRegistrationHub : Hub
    {
        private readonly IAgentRegistry _agentRegistry;
        private readonly IAgentDistributedConfigurationService _agentDistributedConfigurationService;
        private readonly ILogger<AgentRegistrationHub> _logger;

        public AgentRegistrationHub(
            IAgentRegistry agentRegistry,
            IAgentDistributedConfigurationService agentDistributedConfigurationService,
            ILogger<AgentRegistrationHub> logger)
        {
            _agentRegistry = agentRegistry;
            _agentDistributedConfigurationService = agentDistributedConfigurationService;
            _logger = logger;
        }

        public Task<Dictionary<string, string?>> GetDistributedConfiguration()
        {
            if (_agentRegistry.GetAgentByConnectionId(Context.ConnectionId) is null)
            {
                _logger.LogWarning(
                    "Rejected distributed configuration request from unregistered connection {ConnectionId}",
                    Context.ConnectionId);
                throw new HubException("Agent must register before requesting distributed configuration.");
            }

            var snapshot = new Dictionary<string, string?>(
                _agentDistributedConfigurationService.GetConfigurationSnapshot(),
                StringComparer.OrdinalIgnoreCase);

            _logger.LogDebug(
                "Providing {Count} distributed configuration value(s) to agent connection {ConnectionId}",
                snapshot.Count,
                Context.ConnectionId);

            return Task.FromResult(snapshot);
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
                "Agent heartbeat: Node={NodeId}, ConnectionId={ConnectionId}, Health={Health}",
                heartbeat.NodeId,
                connectionId,
                heartbeat.Health);

            _agentRegistry.UpdateAgentHeartbeat(connectionId, heartbeat.Health);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Called by agents to publish their validated managed-container snapshot.
        /// </summary>
        public async Task UpdateManagedContainers(AgentManagedContainerSnapshot snapshot)
        {
            var connectionId = Context.ConnectionId;

            _logger.LogTrace(
                "Managed container snapshot: Node={NodeId}, ConnectionId={ConnectionId}, Count={Count}",
                snapshot.NodeId,
                connectionId,
                snapshot.Containers.Count);

            _agentRegistry.UpdateManagedContainers(connectionId, snapshot);

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
