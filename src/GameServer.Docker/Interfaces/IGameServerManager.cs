using Docker.DotNet;

namespace GameServer.Docker.Interfaces
{
    public interface IGameServerManager
    {
        public Task<List<Models.GameServer>> ListServersAsync();

        public Task<Models.GameServer?> GetServerById(string Id);

        public Task CreateOrUpdateAsync(Models.GameServer server, Models.GameTypeDefinition definition);

        // Service Logs
        public Task<List<string>> GetServiceLogsAsync(string serverId, int tailLines = 100);

        public Task DeleteServer(string serverId, bool deleteData = false);
        
        public Task StartServer(string serverId);

        public Task StopServer(string serverId);

        // Helper methods for SignalR Hubs and Agent Discovery
        public Task<string> GetRunningContainerIdAsync(string serverId);
        
        public Task<string> GetServiceIdAsync(string serverId);

        // Container lookup by Docker labels
        public Task<string?> GetContainerIdByServerIdAsync(string serverId);
        
        public Task<(string? containerId, string? nodeUrl)> GetContainerInfoAsync(string serverId);
    }
}
