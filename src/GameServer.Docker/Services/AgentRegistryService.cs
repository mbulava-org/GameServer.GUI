using GameServer.Docker.Interfaces;
using GameServer.Docker.Models;
using System.Collections.Concurrent;

namespace GameServer.Docker.Services
{
    /// <summary>
    /// In-memory registry for agent connections and container mappings.
    /// Agents register themselves via SignalR and send periodic heartbeats.
    /// This eliminates the need to query Docker Swarm for container-to-node mappings.
    /// </summary>
    public class AgentRegistryService : IAgentRegistry
    {
        private readonly ILogger<AgentRegistryService> _logger;

        // connectionId → NodeAgentEndpoint
        private readonly ConcurrentDictionary<string, NodeAgentEndpoint> _agentsByConnection = new();

        // nodeId → connectionId (for quick lookup by node)
        private readonly ConcurrentDictionary<string, string> _connectionByNode = new();

        // containerId → connectionId (for quick container-to-agent lookup)
        private readonly ConcurrentDictionary<string, string> _containerToConnection = new();

        public AgentRegistryService(ILogger<AgentRegistryService> logger)
        {
            _logger = logger;
        }

        public void RegisterAgent(AgentRegistrationInfo info, string connectionId)
        {
            var endpoint = new NodeAgentEndpoint
            {
                NodeId = info.NodeId,
                NodeName = info.NodeName,
                InternalUrl = info.InternalUrl,
                DiscoveredAt = info.RegisteredAt,
                LastHeartbeat = DateTime.UtcNow,
                IsHealthy = true,
                ConnectionId = connectionId,
                TaskId = string.Empty, // Not used in registration-based system
                ContainerId = string.Empty // Agent's own container ID not needed
            };

            _agentsByConnection[connectionId] = endpoint;
            _connectionByNode[info.NodeId] = connectionId;

            _logger.LogInformation(
                "Agent registered: Node={NodeName} ({NodeId}), ConnectionId={ConnectionId}, Url={Url}, Capabilities={Capabilities}",
                info.NodeName,
                info.NodeId,
                connectionId,
                info.InternalUrl,
                string.Join(", ", info.Capabilities));
        }

        public void UpdateAgentContainers(string connectionId, List<string> containerIds)
        {
            if (!_agentsByConnection.TryGetValue(connectionId, out var agent))
            {
                _logger.LogWarning("Received heartbeat from unknown agent: ConnectionId={ConnectionId}", connectionId);
                return;
            }

            // Update last heartbeat time
            agent.LastHeartbeat = DateTime.UtcNow;
            agent.IsHealthy = true;

            // Remove old container mappings for this agent
            var oldContainers = _containerToConnection
                .Where(kvp => kvp.Value == connectionId)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var oldId in oldContainers)
            {
                _containerToConnection.TryRemove(oldId, out _);
            }

            // Add new container mappings
            foreach (var containerId in containerIds)
            {
                _containerToConnection[containerId] = connectionId;
            }

            _logger.LogDebug(
                "Agent heartbeat: Node={NodeName} ({NodeId}), Containers={ContainerCount} [{ContainerIds}]",
                agent.NodeName,
                agent.NodeId,
                containerIds.Count,
                string.Join(", ", containerIds.Select(id => id.Substring(0, Math.Min(12, id.Length)))));
        }

        public void MarkAgentDisconnected(string connectionId)
        {
            if (!_agentsByConnection.TryRemove(connectionId, out var agent))
            {
                _logger.LogDebug("Attempted to disconnect unknown agent: ConnectionId={ConnectionId}", connectionId);
                return;
            }

            // Remove node mapping
            _connectionByNode.TryRemove(agent.NodeId, out _);

            // Remove all container mappings for this agent
            var containersToRemove = _containerToConnection
                .Where(kvp => kvp.Value == connectionId)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var containerId in containersToRemove)
            {
                _containerToConnection.TryRemove(containerId, out _);
            }

            _logger.LogWarning(
                "Agent disconnected: Node={NodeName} ({NodeId}), ConnectionId={ConnectionId}, Containers={ContainerCount}",
                agent.NodeName,
                agent.NodeId,
                connectionId,
                containersToRemove.Count);
        }

        public NodeAgentEndpoint? GetAgentForContainer(string containerId)
        {
            if (_containerToConnection.TryGetValue(containerId, out var connectionId) &&
                _agentsByConnection.TryGetValue(connectionId, out var agent))
            {
                _logger.LogTrace(
                    "Found agent for container {ContainerId}: Node={NodeName} ({NodeId})",
                    containerId.Substring(0, Math.Min(12, containerId.Length)),
                    agent.NodeName,
                    agent.NodeId);
                return agent;
            }

            _logger.LogDebug(
                "No agent found for container {ContainerId}. Total registered agents: {AgentCount}, Total mapped containers: {ContainerCount}",
                containerId.Substring(0, Math.Min(12, containerId.Length)),
                _agentsByConnection.Count,
                _containerToConnection.Count);
            return null;
        }

        public List<NodeAgentEndpoint> GetAllAgents()
        {
            return _agentsByConnection.Values.ToList();
        }

        public List<NodeAgentEndpoint> GetHealthyAgents()
        {
            return _agentsByConnection.Values
                .Where(a => a.IsHealthy)
                .ToList();
        }

        public NodeAgentEndpoint? GetAgentByNodeId(string nodeId)
        {
            if (_connectionByNode.TryGetValue(nodeId, out var connectionId) &&
                _agentsByConnection.TryGetValue(connectionId, out var agent))
            {
                return agent;
            }
            return null;
        }

        public NodeAgentEndpoint? GetAgentByConnectionId(string connectionId)
        {
            return _agentsByConnection.TryGetValue(connectionId, out var agent) ? agent : null;
        }
    }
}
