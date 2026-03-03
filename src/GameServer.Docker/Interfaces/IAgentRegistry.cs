using GameServer.Docker.Models;

namespace GameServer.Docker.Interfaces
{
    /// <summary>
    /// Service for managing agent registrations and container-to-agent mappings
    /// This replaces the need for Docker Swarm queries to find containers
    /// </summary>
    public interface IAgentRegistry
    {
        /// <summary>
        /// Register or update an agent with the given connection ID
        /// </summary>
        void RegisterAgent(AgentRegistrationInfo info, string connectionId);

        /// <summary>
        /// Update the list of containers on a specific agent
        /// </summary>
        void UpdateAgentContainers(string connectionId, List<string> containerIds);

        /// <summary>
        /// Mark an agent as disconnected
        /// </summary>
        void MarkAgentDisconnected(string connectionId);

        /// <summary>
        /// Get the agent endpoint for a specific container
        /// </summary>
        NodeAgentEndpoint? GetAgentForContainer(string containerId);

        /// <summary>
        /// Get all registered agents
        /// </summary>
        List<NodeAgentEndpoint> GetAllAgents();

        /// <summary>
        /// Get all healthy agents
        /// </summary>
        List<NodeAgentEndpoint> GetHealthyAgents();

        /// <summary>
        /// Get agent by node ID
        /// </summary>
        NodeAgentEndpoint? GetAgentByNodeId(string nodeId);

        /// <summary>
        /// Get agent by connection ID
        /// </summary>
        NodeAgentEndpoint? GetAgentByConnectionId(string connectionId);

        /// <summary>
        /// Get all agents running on manager nodes (for service operations)
        /// </summary>
        List<NodeAgentEndpoint> GetManagerAgents();

        /// <summary>
        /// Get a healthy manager agent (for service operations)
        /// </summary>
        NodeAgentEndpoint? GetHealthyManagerAgent();
    }
}
