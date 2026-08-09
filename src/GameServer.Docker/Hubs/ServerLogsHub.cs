using Microsoft.AspNetCore.SignalR;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Models;
using GameServer.Docker.Services;
using GameServer.Docker.Services.V2;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace GameServer.Docker.Hubs
{
    /// <summary>
    /// SignalR Hub for streaming real-time game server logs to web clients.
    /// Uses Node Agents to locate containers across multiple Docker Swarm nodes.
    /// </summary>
    public class ServerLogsHub : Hub
    {
        private readonly ILogger<ServerLogsHub> _logger;
        private readonly GameServerQueryService _gameServerQueryService;
        private readonly IServerLogAggregator _logAggregator;

        public ServerLogsHub(
            ILogger<ServerLogsHub> logger,
            GameServerQueryService gameServerQueryService,
            IServerLogAggregator logAggregator)
        {
            _logger = logger;
            _gameServerQueryService = gameServerQueryService;
            _logAggregator = logAggregator;
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

            // Get server info from the V2 data store
            var server = await _gameServerQueryService.GetByServerIdAsync(serverId, cancellationToken).ConfigureAwait(false);
            if (server == null)
            {
                _logger.LogWarning("Server {ServerId} not found", serverId);
                yield return "ERROR: Server not found";
                yield break;
            }

            _logger.LogInformation("Server {ServerId} found: Name={Name}, Status={Status}, ServiceName={ServiceName}",
                serverId, server.Name, server.Status, server.ServiceName);

            _logger.LogInformation("Client {ConnectionId} subscribing to shared log stream for server {ServerId}",
                connectionId, serverId);

            await foreach (var logLine in _logAggregator.StreamLogsAsync(
                serverId,
                follow,
                tailLines,
                timestamps,
                cancellationToken).ConfigureAwait(false))
            {
                yield return logLine;
            }

            _logger.LogInformation("Log stream completed for server {ServerId}", serverId);
        }

        /// <summary>
        /// Resolves the actual container ID for a server on the given agent by probing the
        /// agent's REST API with the server-id label filter. Exposed for the log aggregator.
        /// </summary>
        internal static async Task<string?> ResolveContainerIdAsync(
            Models.NodeAgentEndpoint agent,
            string serverId,
            CancellationToken cancellationToken)
        {
            // Use the Node Agent's container listing endpoint with a label filter.
            // The agent mirrors Docker's list endpoint and returns containers whose labels match.
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var url = $"{agent.InternalUrl}/containers?label={Uri.EscapeDataString($"{GameServer.Docker.Constants.ServiceLabels.ServerId}={serverId}")}";

            var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            {
                return null;
            }

            if (doc.RootElement[0].TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
            {
                return idProp.GetString();
            }

            if (doc.RootElement[0].TryGetProperty("Id", out var idPropUpper) && idPropUpper.ValueKind == JsonValueKind.String)
            {
                return idPropUpper.GetString();
            }

            return null;
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


