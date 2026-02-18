using Microsoft.AspNetCore.SignalR;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Services;
using System.Runtime.CompilerServices;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace GameServer.Docker.Hubs
{
    /// <summary>
    /// SignalR Hub for streaming real-time game server logs to web clients.
    /// Streams logs directly from Docker containers using Docker.DotNet.
    /// </summary>
    /// </summary>
    public class ServerLogsHub : Hub
    {
        private readonly ILogger<ServerLogsHub> _logger;
        private readonly NodeAgentClient _agentClient;
        private readonly INodeAgentDiscovery _nodeDiscovery;
        private readonly IGameServerManager _serverManager;
        private readonly IDockerClient _dockerClient;

        public ServerLogsHub(
            ILogger<ServerLogsHub> logger,
            NodeAgentClient agentClient,
            INodeAgentDiscovery nodeDiscovery,
            IGameServerManager serverManager,
            IDockerClient dockerClient)
        {
            _logger = logger;
            _agentClient = agentClient;
            _nodeDiscovery = nodeDiscovery;
            _serverManager = serverManager;
            _dockerClient = dockerClient;
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

            // Get server info
            var server = await _serverManager.GetServerById(serverId);
            if (server == null)
            {
                _logger.LogWarning("Server {ServerId} not found", serverId);
                yield return "ERROR: Server not found";
                yield break;
            }

            // Use the ContainerId from the server model (populated when server status is fetched)
            var containerId = server.ContainerId;
            
            // Fallback: Try other lookup methods if ContainerId is not available
            if (string.IsNullOrEmpty(containerId))
            {
                _logger.LogWarning("ContainerId not available on server model, trying fallback lookups for server {ServerId}", serverId);
                
                // Method 1: Try label-based lookup
                (containerId, _) = await _serverManager.GetContainerInfoAsync(serverId);

                // Method 2: Try service/task-based lookup
                if (string.IsNullOrEmpty(containerId))
                {
                    try
                    {
                        containerId = await _serverManager.GetRunningContainerIdAsync(serverId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Fallback container lookup failed for server {ServerId}", serverId);
                    }
                }
            }

            if (string.IsNullOrEmpty(containerId))
            {
                _logger.LogWarning("Could not find running container for server {ServerId}. Server state: {Status}, ServiceName: {ServiceName}", 
                    serverId, server.Status, server.ServiceName);
                    
                yield return $"ERROR: Could not locate running container for server '{server.Name}' (ID: {serverId}).";
                yield return "";
                yield return "Possible reasons:";
                yield return "  • The container is not running yet (check server status)";
                yield return "  • The container failed to start (check Docker logs)";
                yield return $"  • Service name: {server.ServiceName}";
                yield return $"  • Server status: {server.Status}";
                yield break;
            }

            _logger.LogInformation("Streaming logs for server {ServerId}, container {ContainerId}",
                serverId, containerId);

            // Stream logs directly from Docker daemon
            ContainerLogsParameters logsParameters;
            MultiplexedStream? logStream = null;

            try
            {
                logsParameters = new ContainerLogsParameters
                {
                    ShowStdout = true,
                    ShowStderr = true,
                    Follow = follow,
                    Timestamps = timestamps,
                    Tail = tailLines > 0 ? tailLines.ToString() : "all"
                };

                logStream = await _dockerClient.Containers.GetContainerLogsAsync(
                    containerId,
                    false, // tty: false
                    logsParameters,
                    cancellationToken);

                var buffer = new byte[4096];
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    var result = await logStream.ReadOutputAsync(buffer, 0, buffer.Length, cancellationToken);
                    
                    if (result.EOF)
                        break;

                    // Convert bytes to string
                    var line = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                    
                    if (!string.IsNullOrEmpty(line))
                    {
                        yield return line;
                    }
                }

                _logger.LogInformation("Log stream completed for server {ServerId}", serverId);
            }
            finally
            {
                logStream?.Dispose();
                
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
        /// Queries the Node Agent to find the container by service name or server ID
        /// </summary>
        private async Task<string?> GetContainerIdForServer(string nodeUrl, string serviceId)
        {
            try
            {
                // Try to get container list from the Node Agent
                // The NodeAgentClient should have a method to list containers
                // For now, we'll use the serviceId as a filter
                
                // If serviceId is actually a container ID already, return it
                if (!string.IsNullOrEmpty(serviceId) && serviceId.Length >= 12)
                {
                    // Docker container IDs are typically 64 characters (full) or 12+ characters (short)
                    // If it looks like a container ID, try using it directly
                    if (serviceId.All(c => char.IsLetterOrDigit(c)))
                    {
                        _logger.LogDebug("ServiceId {ServiceId} looks like a container ID, using directly", serviceId);
                        return serviceId;
                    }
                }

                // Otherwise, we need to query the agent to find the container
                // This would require a method in NodeAgentClient to list containers by label or name
                // For now, log and return the serviceId hoping it's a valid container ID
                _logger.LogDebug("Attempting to use serviceId {ServiceId} as container ID", serviceId);
                return serviceId;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve container ID for service {ServiceId} on node {NodeUrl}", 
                    serviceId, nodeUrl);
                return null;
            }
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

