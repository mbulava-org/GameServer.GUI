using System.Runtime.CompilerServices;
using System.Text.Json;
using GameServer.API.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace GameServer.Monitoring.Host.Hubs;

/// <summary>
/// SignalR Hub for shared container attach streams.
/// Moved from GameServer.API in Phase 2 — Monitoring service owns container attach.
/// Multiple clients viewing the same container share one underlying agent attach WebSocket.
/// The first subscriber to send input becomes the controller.
/// </summary>
public class ContainerAttachHub : Hub
{
    private readonly ILogger<ContainerAttachHub> _logger;
    private readonly IContainerAttachAggregator _attachAggregator;
    private readonly IServerResourceMonitor _serverResourceMonitor;

    public ContainerAttachHub(
        ILogger<ContainerAttachHub> logger,
        IContainerAttachAggregator attachAggregator,
        IServerResourceMonitor serverResourceMonitor)
    {
        _logger = logger;
        _attachAggregator = attachAggregator;
        _serverResourceMonitor = serverResourceMonitor;
    }

    /// <summary>
    /// Subscribe to a shared attach stream. Resolves container from server ID if containerId is null.
    /// </summary>
    public async IAsyncEnumerable<string> SubscribeToContainer(
        string serverId,
        string? containerId = null,
        bool timestamps = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var connectionId = Context.ConnectionId;

        var resolvedContainerId = containerId;
        if (string.IsNullOrWhiteSpace(resolvedContainerId))
            resolvedContainerId = await ResolveContainerIdFromServerAsync(serverId, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(resolvedContainerId))
        {
            _logger.LogWarning("Attach subscription failed: could not resolve container for server {ServerId}", serverId);
            yield return SerializeFrame(AttachFrameKind.Error, "Could not resolve container for server");
            yield break;
        }

        _logger.LogInformation("Client {ConnectionId} subscribing to attach stream for container {ContainerId} (server {ServerId})",
            connectionId, resolvedContainerId, serverId);

        await foreach (var frame in _attachAggregator.SubscribeAsync(connectionId, resolvedContainerId, cancellationToken).ConfigureAwait(false))
        {
            yield return SerializeFrame(frame.Kind, frame.Payload);
        }

        _logger.LogInformation("Client {ConnectionId} attach stream ended for container {ContainerId}", connectionId, resolvedContainerId);
    }

    /// <summary>Send input to the shared attach stream.</summary>
    public async Task<bool> SendInput(string containerId, string input)
    {
        _logger.LogTrace("Client {ConnectionId} sending attach input for container {ContainerId}", Context.ConnectionId, containerId);
        return await _attachAggregator.SendInputAsync(Context.ConnectionId, containerId, input, Context.ConnectionAborted).ConfigureAwait(false);
    }

    /// <summary>Disconnect from the shared attach stream.</summary>
    public async Task DisconnectFromContainer(string containerId)
        => await _attachAggregator.UnsubscribeAsync(Context.ConnectionId, containerId).ConfigureAwait(false);

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Client {ConnectionId} disconnected from attach hub", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    private static string SerializeFrame(AttachFrameKind kind, string payload)
        => JsonSerializer.Serialize(new AttachStreamMessage(kind, payload));

    private async Task<string?> ResolveContainerIdFromServerAsync(string serverId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(serverId)) return null;
        try
        {
            var snapshot = await _serverResourceMonitor.GetSnapshotAsync(serverId, cancellationToken).ConfigureAwait(false);
            var cid = snapshot?.ContainerIds.FirstOrDefault() ?? snapshot?.RealTimeStats?.ContainerId;
            if (!string.IsNullOrWhiteSpace(cid))
            {
                _logger.LogInformation("Resolved container {ContainerId} for server {ServerId}", cid, serverId);
                return cid;
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to resolve container ID for server {ServerId}", serverId); }
        return null;
    }
}

/// <summary>Wire format for attach stream messages sent to clients.</summary>
public sealed record AttachStreamMessage(AttachFrameKind Kind, string Payload);
