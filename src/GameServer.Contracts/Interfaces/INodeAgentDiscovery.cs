using GameServer.API.Models;

namespace GameServer.API.Interfaces
{
    /// <summary>
    /// Service for discovering and communicating with node agents in the Docker Swarm cluster.
    /// 
    /// Node Agents are the ONLY source for real-time container statistics and logs.
    /// They run on each worker node and have direct access to container runtime metrics.
    /// 
    /// Do NOT use direct Docker client calls for container stats - they require access to the
    /// specific Docker daemon where the container is running, which is not available from the Swarm Manager.
    /// </summary>
    public interface INodeAgentDiscovery
    {
        /// <summary>
        /// Discover all healthy agent endpoints in the swarm
        /// </summary>
        Task<List<NodeAgentEndpoint>> DiscoverAgentsAsync();

        /// <summary>
        /// Find the agent endpoint for a specific container
        /// </summary>
        Task<NodeAgentEndpoint?> GetAgentForContainerAsync(string containerId);

        /// <summary>
        /// Find the agent endpoint for a specific server (finds running container first)
        /// </summary>
        Task<NodeAgentEndpoint?> GetAgentForServerAsync(string serverId);

        /// <summary>
        /// Get real-time container stats from the appropriate node agent.
        /// This is the PRIMARY method for obtaining container resource usage (CPU%, memory%, I/O, etc.).
        /// Returns null if no agent is available or container is not running.
        /// </summary>
        Task<ContainerStats?> GetContainerStatsAsync(string containerId);

        /// <summary>
        /// Stream real-time container stats from the appropriate node agent.
        /// Provides continuous updates of container resource usage (CPU%, memory%, I/O, etc.).
        /// The stream will continue until cancellation is requested or the container is no longer available.
        /// </summary>
        IAsyncEnumerable<ContainerStats> StreamContainerStatsAsync(string containerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get real-time container logs from the appropriate node agent.
        /// This provides direct access to the container's stdout/stderr streams.
        /// Returns null if no agent is available or container is not running.
        /// </summary>
        Task<List<string>?> GetContainerLogsAsync(string containerId, int tailLines = 1000);

        /// <summary>
        /// Get Docker Swarm service logs (aggregated from all replicas/tasks).
        /// This must be retrieved from a manager node agent.
        /// Returns null if no manager agent is available.
        /// </summary>
        Task<List<string>?> GetServiceLogsAsync(string serviceId, int tailLines = 1000);
    }
}
