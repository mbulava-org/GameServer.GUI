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

            // Use label-based lookup to find container
            _logger.LogInformation("Looking up container for server {ServerId} using Docker labels", serverId);
            var (containerId, nodeUrl) = await _serverManager.GetContainerInfoAsync(serverId);

            // Fallback: Try to use existing method if label lookup fails
            if (string.IsNullOrEmpty(containerId))
            {
                _logger.LogWarning("Label-based lookup failed, trying legacy method for server {ServerId}", serverId);
                try
                {
                    containerId = await _serverManager.GetRunningContainerIdAsync(serverId);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Legacy container lookup also failed for server {ServerId}", serverId);
                }
            }

            if (string.IsNullOrEmpty(containerId))
            {
                _logger.LogWarning("Could not find container for server {ServerId}", serverId);
                yield return $"ERROR: Could not locate running container for server {serverId}. Make sure the server is started.";
                yield break;
            }

            _logger.LogInformation("Streaming logs for server {ServerId}, container {ContainerId}",
                serverId, containerId);

            // Stream logs directly from Docker daemon
            ContainerLogsParameters logsParameters;
            Stream? logStream = null;
            StreamReader? reader = null;

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
                    logsParameters,
                    cancellationToken);

                reader = new StreamReader(logStream);
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null)
                        break;

                    // Clean Docker log line (remove 8-byte header if present)
                    var cleanLine = CleanDockerLogLine(line);
                    
                    if (!string.IsNullOrEmpty(cleanLine))
                    {
                        yield return cleanLine;
                    }
                }

                _logger.LogInformation("Log stream completed for server {ServerId}", serverId);
            }
            finally
            {
                reader?.Dispose();
                logStream?.Dispose();
                
                _logger.LogInformation("Log stream ended for server {ServerId} on connection {ConnectionId}",
                    serverId, connectionId);
            }
        }

        /// <summary>
        /// Clean Docker log line by removing 8-byte header if present
        /// </summary>
        private string CleanDockerLogLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                return string.Empty;

            // Docker logs may have 8-byte header: [stream_type(1)][padding(3)][size(4)]
            // If line starts with non-printable characters, skip the first 8 bytes
            if (line.Length > 8 && line[0] < 32)
            {
                return line.Substring(8);
            }

            return line;
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
