using Microsoft.AspNetCore.SignalR;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Services;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR.Client;

namespace GameServer.Docker.Hubs
{
    /// <summary>
    /// SignalR Hub for streaming real-time game server logs to web clients.
    /// Uses Node Agents to locate containers across multiple Docker Swarm nodes.
    /// </summary>
    public class ServerLogsHub : Hub
    {
        private readonly ILogger<ServerLogsHub> _logger;
        private readonly IGameServerManager _serverManager;
        private readonly INodeAgentDiscovery _nodeAgentDiscovery;
        private readonly IHttpClientFactory _httpClientFactory;

        public ServerLogsHub(
            ILogger<ServerLogsHub> logger,
            IGameServerManager serverManager,
            INodeAgentDiscovery nodeAgentDiscovery,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _serverManager = serverManager;
            _nodeAgentDiscovery = nodeAgentDiscovery;
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Stream real-time logs from a game server container via Node Agent
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
            _logger.LogInformation("Client {ConnectionId} starting log stream for server {ServerId} (follow={Follow}, tail={Tail})", 
                connectionId, serverId, follow, tailLines);

            // Get server info
            var server = await _serverManager.GetServerById(serverId);
            if (server == null)
            {
                _logger.LogWarning("Server {ServerId} not found", serverId);
                yield return "ERROR: Server not found";
                yield break;
            }

            _logger.LogInformation("Server {ServerId} found: Name={Name}, Status={Status}, ContainerId={ContainerId}",
                serverId, server.Name, server.Status, server.ContainerId ?? "(null)");

            // Get fresh container ID
            var containerId = server.ContainerId;
            if (string.IsNullOrEmpty(containerId))
            {
                _logger.LogWarning("ContainerId not available, attempting to refresh for server {ServerId}", serverId);
                try
                {
                    containerId = await _serverManager.GetRunningContainerIdAsync(serverId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to get container ID for server {ServerId}", serverId);
                }
            }

            if (string.IsNullOrEmpty(containerId))
            {
                _logger.LogWarning("Could not find running container for server {ServerId}", serverId);
                yield return $"ERROR: Could not locate running container for server '{server.Name}'.";
                yield return "The server may not be started yet or has stopped.";
                yield break;
            }

            // Find which Node Agent has this container
            _logger.LogInformation("Looking for container {ContainerId} across Node Agents", containerId);
            var agent = await _nodeAgentDiscovery.GetAgentForContainerAsync(containerId);
            
            if (agent == null)
            {
                _logger.LogWarning("No Node Agent found with container {ContainerId} for server {ServerId}", 
                    containerId, serverId);
                yield return $"ERROR: Container {containerId.Substring(0, 12)}... not found on any node.";
                yield return "The container may have been removed or is not accessible.";
                yield break;
            }

            _logger.LogInformation("Found container {ContainerId} on Node Agent {AgentUrl}", 
                containerId, agent.InternalUrl);

            // Stream logs from the Node Agent
            await foreach (var logLine in StreamLogsFromAgentAsync(
                agent.InternalUrl, 
                containerId, 
                follow, 
                tailLines, 
                timestamps, 
                cancellationToken))
            {
                yield return logLine;
            }

            _logger.LogInformation("Log stream completed for server {ServerId}", serverId);
        }

        /// <summary>
        /// Stream logs from a Node Agent's SignalR hub
        /// </summary>
        private async IAsyncEnumerable<string> StreamLogsFromAgentAsync(
            string agentUrl,
            string containerId,
            bool follow,
            int tailLines,
            bool timestamps,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var hubUrl = $"{agentUrl}/hubs/nodeagent";
            _logger.LogInformation("Connecting to Node Agent hub at {HubUrl} for container {ContainerId}", 
                hubUrl, containerId);

            var connection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .ConfigureLogging(logging =>
                {
                    logging.SetMinimumLevel(LogLevel.Warning);
                })
                .Build();

            await connection.StartAsync(cancellationToken);
            _logger.LogInformation("Connected to Node Agent, streaming logs for container {ContainerId}", containerId);

            try
            {
                // Call the Node Agent's StreamContainerLogs method
                await foreach (var logLine in connection.StreamAsync<string>(
                    "StreamContainerLogs",
                    containerId,
                    follow,
                    tailLines,
                    timestamps,
                    cancellationToken))
                {
                    yield return logLine;
                }

                _logger.LogDebug("Agent log stream completed for container {ContainerId}", containerId);
            }
            finally
            {
                await connection.DisposeAsync();
                _logger.LogDebug("Disconnected from Node Agent hub");
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


