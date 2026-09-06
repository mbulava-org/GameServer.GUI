using Microsoft.AspNetCore.SignalR;
using GameServer.API.Interfaces;
using GameServer.API.Models;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace GameServer.Monitoring.Host.Hubs;

/// <summary>
/// SignalR Hub for streaming real-time game server logs to web clients.
/// Moved from GameServer.API in Phase 2 — Monitoring service owns this stream.
/// Uses IServerLogAggregator which routes to the correct agent per node.
/// </summary>
public class ServerLogsHub : Hub
{
    private readonly ILogger<ServerLogsHub> _logger;
    private readonly IGameServerQueryService _gameServerQueryService;
    private readonly IServerLogAggregator _logAggregator;

    public ServerLogsHub(
        ILogger<ServerLogsHub> logger,
        IGameServerQueryService gameServerQueryService,
        IServerLogAggregator logAggregator)
    {
        _logger = logger;
        _gameServerQueryService = gameServerQueryService;
        _logAggregator = logAggregator;
    }

    /// <summary>Stream real-time logs from a game server container via Node Agent.</summary>
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

        var server = await _gameServerQueryService.GetByServerIdAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server == null)
        {
            _logger.LogWarning("Server {ServerId} not found", serverId);
            yield return "ERROR: Server not found";
            yield break;
        }

        _logger.LogInformation("Client {ConnectionId} subscribing to log stream for server {ServerId}", connectionId, serverId);

        await foreach (var logLine in _logAggregator.StreamLogsAsync(serverId, follow, tailLines, timestamps, cancellationToken).ConfigureAwait(false))
        {
            yield return logLine;
        }

        _logger.LogInformation("Log stream completed for server {ServerId}", serverId);
    }

    /// <summary>Resolves the actual container ID for a server by querying the agent REST API.</summary>
    internal static async Task<string?> ResolveContainerIdAsync(
        NodeAgentEndpoint agent,
        string serverId,
        CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var url = $"{agent.InternalUrl}/containers?label={Uri.EscapeDataString($"{GameServer.API.Constants.ServiceLabels.ServerId}={serverId}")}";

        var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0) return null;

        if (doc.RootElement[0].TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
            return idProp.GetString();

        if (doc.RootElement[0].TryGetProperty("Id", out var idPropUpper) && idPropUpper.ValueKind == JsonValueKind.String)
            return idPropUpper.GetString();

        return null;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        if (exception != null)
            _logger.LogWarning(exception, "Client {ConnectionId} disconnected with error", connectionId);
        else
            _logger.LogDebug("Client {ConnectionId} disconnected", connectionId);

        await base.OnDisconnectedAsync(exception);
    }
}
