using Docker.DotNet;
using Docker.DotNet.Models;
using GameServer.Docker.Interfaces;
using System.Text;

namespace GameServer.Docker.Services
{
    public class GameServerManagerService(ILogger<GameServerManagerService> logger,
        DockerServiceHelper dockerServiceHelper) : IGameServerManager
    {
        

        
        public async Task<List<Models.GameServer>> ListServersAsync()
        {
            return await dockerServiceHelper.ListGameServersAsync();
        }

        public async Task<Models.GameServer?> GetServerById(string Id)
        {
            return await dockerServiceHelper.GetGameServerById(Id);
        }

        public async Task CreateOrUpdateAsync(Models.GameServer server, Models.GameTypeDefinition definition)
        {
            await dockerServiceHelper.CreateOrUpdateGameServerAsync(server, definition);
        }

        public async Task DeleteServer(string Id, bool deleteData = false)
        {
            logger.LogInformation("Deleting server {ServerId} (deleteData: {DeleteData})", Id, deleteData);

            // Get server details before deletion (to get volume paths)
            var server = await dockerServiceHelper.GetGameServerById(Id);
            if (server == null)
            {
                logger.LogWarning("Server {ServerId} not found", Id);
                throw new InvalidOperationException($"Server {Id} not found");
            }

            // Delete the Docker service
            logger.LogInformation("Removing Docker service for server {ServerId}", Id);
            await dockerServiceHelper.DeleteGameServerAsync(Id);

            // Optionally delete data paths
            if (deleteData && server.Volumes != null && server.Volumes.Any())
            {
                logger.LogInformation("Deleting data paths for server {ServerId}", Id);
                foreach (var volume in server.Volumes)
                {
                    if (!string.IsNullOrEmpty(volume.Source) && Directory.Exists(volume.Source))
                    {
                        try
                        {
                            logger.LogInformation("Deleting directory: {Path}", volume.Source);
                            Directory.Delete(volume.Source, recursive: true);
                            logger.LogInformation("Successfully deleted directory: {Path}", volume.Source);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to delete directory: {Path}", volume.Source);
                            // Continue with other deletions even if one fails
                        }
                    }
                }
            }

            logger.LogInformation("Server {ServerId} deleted successfully", Id);
        }
      
        #region Service Logs

        public async Task<List<string>> GetServiceLogsAsync(string serverId, int tailLines = 1000)
        {
            return await dockerServiceHelper.GetGameServerServiceLogsAsync(serverId, tailLines);
        }

        #endregion

        #region Service Control

        public async Task StartServer(string serverId)
        {
            await dockerServiceHelper.StartGameServerAsync(serverId);
        }   
        public async Task StopServer(string serverId)
        {
            await dockerServiceHelper.StopGameServerAsync(serverId);
        }

        #endregion

        #region Helper Methods

        public async Task<string> GetRunningContainerIdAsync(string serverId)
        {
            return await dockerServiceHelper.GetRunningContainerIdForGameServerAsync(serverId);
        }

        public async Task<string> GetServiceIdAsync(string serverId)
        {
            return await dockerServiceHelper.GetGameServerServiceIdAsync(serverId);
        }

        #endregion

    }
}
