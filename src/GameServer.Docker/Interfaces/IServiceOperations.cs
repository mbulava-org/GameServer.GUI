using Docker.DotNet.Models;

namespace GameServer.Docker.Interfaces
{
    /// <summary>
    /// Abstraction for Docker Swarm service management operations.
    /// Implementations can use direct Docker client or delegate to a manager node agent.
    /// </summary>
    public interface IServiceOperations
    {
        /// <summary>
        /// Create a new Docker Swarm service
        /// </summary>
        Task<ServiceCreateResponse> CreateServiceAsync(ServiceCreateParameters parameters, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update an existing Docker Swarm service
        /// </summary>
        Task UpdateServiceAsync(string serviceId, ServiceUpdateParameters parameters, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete a Docker Swarm service
        /// </summary>
        Task RemoveServiceAsync(string serviceId, CancellationToken cancellationToken = default);

        /// <summary>
        /// List all Docker Swarm services
        /// </summary>
        Task<IList<SwarmService>> ListServicesAsync(string? labelFilter = null, string? serviceName = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get detailed information about a specific service
        /// </summary>
        Task<SwarmService> InspectServiceAsync(string serviceId, CancellationToken cancellationToken = default);

        /// <summary>
        /// List Docker Swarm tasks
        /// </summary>
        Task<IList<TaskResponse>> ListTasksAsync(TasksListParameters? parameters = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// List Docker networks
        /// </summary>
        Task<IList<NetworkResponse>> ListNetworksAsync(NetworksListParameters? parameters = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Inspect a Docker network
        /// </summary>
        Task<NetworkResponse> InspectNetworkAsync(string networkId, CancellationToken cancellationToken = default);
    }
}
