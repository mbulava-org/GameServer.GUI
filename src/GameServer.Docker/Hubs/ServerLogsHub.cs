using Microsoft.AspNetCore.SignalR;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Services;
using System.Runtime.CompilerServices;

namespace GameServer.Docker.Hubs
{
    /// <summary>
    /// SignalR Hub for streaming real-time game server logs to web clients.
    /// Acts as a proxy between web clients and Node Agent hubs.
    /// </summary>
    public class ServerLogsHub : Hub
    {
        private readonly ILogger<ServerLogsHub> _logger;
        private readonly NodeAgentClient _agentClient;
        private readonly INodeAgentDiscovery _nodeDiscovery;
        private readonly IGameServerManager _serverManager;

        public ServerLogsHub(
            ILogger<ServerLogsHub> logger,
            NodeAgentClient agentClient,
            INodeAgentDiscovery nodeDiscovery,
            IGameServerManager serverManager)
        {
            _logger = logger;
            _agentClient = agentClient;
            _nodeDiscovery = nodeDiscovery;
            _serverManager = serverManager;
        }

        /// <summary>
        /// Stream real-time logs from a game server container
        /// </summary>
        /// <param name="serverId">Game server ID</param>
        /// <param name="follow">Continuously stream new logs</param>
        /// <param name="tailLines">Number of recent lines to include</param>
        /// <param name="timestamps">Include timestamps</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async IAsyncEnumerable<string> StreamServerLogs(
            string serverId,
            bool follow = true,
            int tailLines = 100,
            bool timestamps = true,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var connectionId = Context.ConnectionId;
            _logger.LogInformation("Client {ConnectionId} starting log stream for server {ServerId}", 
                connectionId, serverId);

            try
            {
                // Get server info
                var server = await _serverManager.GetServerById(serverId);
                if (server == null)
                {
                    _logger.LogWarning("Server {ServerId} not found", serverId);
                    yield break;
                }

                // Find which node this server is running on
                var nodeAgents = await _nodeDiscovery.DiscoverAgentsAsync();
                string? agentUrl = null;
                string? containerId = null;

                foreach (var agent in nodeAgents)
                {
                    try
                    {
                        // Check if this server's container is on this node
                        // This would ideally come from your server tracking/orchestration
                        // For now, we'll try to find it by service name
                        var serviceId = server.ServiceName;
                        
                        // Get container ID from Docker service
                        // You may need to add a method to DockerServiceHelper to resolve this
                        containerId = await GetContainerIdForServer(agent.InternalUrl, serviceId);
                        
                        if (!string.IsNullOrEmpty(containerId))
                        {
                            agentUrl = agent.InternalUrl;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Server {ServerId} not found on node {NodeUrl}", 
                            serverId, agent.InternalUrl);
                    }
                }

                if (string.IsNullOrEmpty(agentUrl) || string.IsNullOrEmpty(containerId))
                {
                    _logger.LogWarning("Could not find container for server {ServerId}", serverId);
                    yield return $"ERROR: Could not locate container for server {serverId}";
                    yield break;
                }

                _logger.LogInformation("Streaming logs for server {ServerId} from node {NodeUrl}, container {ContainerId}",
                    serverId, agentUrl, containerId);

                // Stream logs from the Node Agent
                await foreach (var logLine in _agentClient.StreamContainerLogsAsync(
                    agentUrl, 
                    containerId, 
                    follow, 
                    tailLines, 
                    timestamps, 
                    cancellationToken))
                {
                    yield return logLine;
                }
            }
            finally
            {
                _logger.LogInformation("Log stream ended for server {ServerId} on connection {ConnectionId}",
                    serverId, connectionId);
            }
        }

        /// <summary>
        /// Stream real-time container statistics for a game server
        /// </summary>
        public async IAsyncEnumerable<object> StreamServerStats(
            string serverId,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var connectionId = Context.ConnectionId;
            _logger.LogInformation("Client {ConnectionId} starting stats stream for server {ServerId}",
                connectionId, serverId);

            try
            {
                var server = await _serverManager.GetServerById(serverId);
                if (server == null)
                {
                    _logger.LogWarning("Server {ServerId} not found", serverId);
                    yield break;
                }

                var nodeAgents = await _nodeDiscovery.DiscoverAgentsAsync();
                string? agentUrl = null;
                string? containerId = null;

                foreach (var agent in nodeAgents)
                {
                    try
                    {
                        var serviceId = server.ServiceName;
                        containerId = await GetContainerIdForServer(agent.InternalUrl, serviceId);

                        if (!string.IsNullOrEmpty(containerId))
                        {
                            agentUrl = agent.InternalUrl;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Server {ServerId} not found on node {NodeUrl}",
                            serverId, agent.InternalUrl);
                    }
                }

                if (string.IsNullOrEmpty(agentUrl) || string.IsNullOrEmpty(containerId))
                {
                    _logger.LogWarning("Could not find container for server {ServerId}", serverId);
                    yield break;
                }

                _logger.LogInformation("Streaming stats for server {ServerId} from node {NodeUrl}, container {ContainerId}",
                    serverId, agentUrl, containerId);

                await foreach (var stats in _agentClient.StreamContainerStatsAsync(
                    agentUrl,
                    containerId,
                    cancellationToken))
                {
                    yield return stats;
                }
            }
            finally
            {
                _logger.LogInformation("Stats stream ended for server {ServerId} on connection {ConnectionId}",
                    serverId, connectionId);
            }
        }

        /// <summary>
        /// Helper method to get container ID for a server
        /// TODO: This should be moved to a proper service that tracks container-to-server mappings
        /// </summary>
        private async Task<string?> GetContainerIdForServer(string nodeUrl, string serviceId)
        {
            // This is a placeholder - you'll need to implement proper container resolution
            // Options:
            // 1. Store container IDs when servers are created
            // 2. Query Docker Swarm API to resolve service -> container
            // 3. Use labels on containers to track server IDs
            
            // For now, return null and let the caller handle it
            // In production, you'd query the Node Agent's container list endpoint
            return await Task.FromResult<string?>(null);
        }

        /// <summary>
        /// Called when a client disconnects
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;

            if (exception != null)
            {
                _logger.LogWarning(exception, "Client {ConnectionId} disconnected with error", connectionId);
            }
            else
            {
                _logger.LogDebug("Client {ConnectionId} disconnected", connectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
