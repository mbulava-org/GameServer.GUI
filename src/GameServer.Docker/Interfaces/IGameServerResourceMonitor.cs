namespace GameServer.Docker.Interfaces
{
    /// <summary>
    /// Interface for monitoring game server resource usage
    /// </summary>
    public interface IGameServerResourceMonitor
    {
        /// <summary>
        /// Get current resource usage snapshot for a server
        /// </summary>
        Task<Models.ServerResourceUsage?> GetResourceUsageAsync(string serverId);

        /// <summary>
        /// Stream resource usage updates for a server
        /// </summary>
        IAsyncEnumerable<Models.ServerResourceUsage> StreamResourceUsageAsync(
            string serverId,
            CancellationToken cancellationToken = default);
    }
}