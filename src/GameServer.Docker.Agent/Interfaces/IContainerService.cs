using Docker.DotNet.Models;

namespace GameServer.Docker.Agent.Interfaces
{
    /// <summary>
    /// Service for interacting with Docker containers on the local node
    /// </summary>
    public interface IContainerService
    {
        /// <summary>
        /// Get real-time statistics for a container
        /// </summary>
        Task<Models.ContainerStatsResponse> GetContainerStatsAsync(string containerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stream real-time statistics for a container using Docker's native streaming API.
        /// Uses IProgress callbacks converted to async enumerable for efficient streaming.
        /// </summary>
        IAsyncEnumerable<Models.ContainerStatsResponse> StreamContainerStatsAsync(string containerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get logs from a container
        /// </summary>
        Task<Models.ContainerLogsResponse> GetContainerLogsAsync(string containerId, int tailLines = 100, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stream real-time logs from a container using Docker's native log streaming API.
        /// Continuously yields log lines as they are produced by the container.
        /// </summary>
        /// <param name="containerId">Container ID to stream logs from</param>
        /// <param name="follow">If true, continues streaming new logs. If false, returns historical logs only.</param>
        /// <param name="tailLines">Number of recent lines to include (0 for all)</param>
        /// <param name="timestamps">Include timestamps in log output</param>
        /// <param name="cancellationToken">Cancellation token</param>
        IAsyncEnumerable<string> StreamContainerLogsAsync(
            string containerId, 
            bool follow = true, 
            int tailLines = 100, 
            bool timestamps = true,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Inspect a container to get detailed information
        /// </summary>
        Task<Models.ContainerInspectResponse> InspectContainerAsync(string containerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// List all running containers on this node
        /// </summary>
        Task<Models.ContainerListResponse> ListContainersAsync(CancellationToken cancellationToken = default);
    }
}
