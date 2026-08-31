using System.Runtime.CompilerServices;
using System.Text.Json;
using GameServer.API.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace GameServer.API.Hubs
{
    /// <summary>
    /// SignalR Hub for shared container attach streams.
    /// Multiple clients viewing the same container share one underlying agent attach WebSocket.
    /// The first subscriber to send input becomes the controller; others see output and are
    /// notified who has control.
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
        /// Subscribe to a shared attach stream. If <paramref name="containerId"/> is null,
        /// the server resolves the container from the server ID.
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
            {
                resolvedContainerId = await ResolveContainerIdFromServerAsync(serverId, cancellationToken).ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(resolvedContainerId))
            {
                _logger.LogWarning("Attach subscription failed: could not resolve container for server {ServerId}", serverId);
                yield return SerializeFrame(AttachFrameKind.Error, "Could not resolve container for server");
                yield break;
            }

            _logger.LogInformation(
                "Client {ConnectionId} subscribing to shared attach stream for container {ContainerId} (server {ServerId})",
                connectionId, resolvedContainerId, serverId);

            await foreach (var frame in _attachAggregator.SubscribeAsync(connectionId, resolvedContainerId, cancellationToken).ConfigureAwait(false))
            {
                yield return SerializeFrame(frame.Kind, frame.Payload);
            }

            _logger.LogInformation("Client {ConnectionId} attach stream ended for container {ContainerId}", connectionId, resolvedContainerId);
        }

        /// <summary>
        /// Send input to the shared attach stream. The first caller to send input becomes the
        /// controller. Subsequent callers are ignored unless control has been released.
        /// </summary>
        public async Task<bool> SendInput(string containerId, string input)
        {
            var connectionId = Context.ConnectionId;
            _logger.LogTrace("Client {ConnectionId} sending attach input for container {ContainerId}", connectionId, containerId);
            return await _attachAggregator.SendInputAsync(connectionId, containerId, input, Context.ConnectionAborted).ConfigureAwait(false);
        }

        /// <summary>
        /// Disconnect from the shared attach stream.
        /// </summary>
        public async Task DisconnectFromContainer(string containerId)
        {
            await _attachAggregator.UnsubscribeAsync(Context.ConnectionId, containerId).ConfigureAwait(false);
        }

        /// <summary>
        /// Called when a client disconnects from any hub path.
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;
            _logger.LogDebug("Client {ConnectionId} disconnected from attach hub", connectionId);

            try
            {
                // Best-effort unsubscribe from any active attach streams. If a subscriber
                // never explicitly disconnected, this cleans up resources and releases control.
                // The aggregator uses exact container IDs; we cannot enumerate here, so callers
                // should prefer DisconnectFromContainer when leaving a specific stream.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during attach disconnect for {ConnectionId}", connectionId);
            }

            await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
        }

        private static string SerializeFrame(AttachFrameKind kind, string payload)
        {
            return JsonSerializer.Serialize(new AttachStreamMessage(kind, payload));
        }

        private async Task<string?> ResolveContainerIdFromServerAsync(string serverId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(serverId))
            {
                return null;
            }

            try
            {
                var snapshot = await _serverResourceMonitor.GetSnapshotAsync(serverId, cancellationToken).ConfigureAwait(false);
                var containerId = snapshot?.ContainerIds.FirstOrDefault() ?? snapshot?.RealTimeStats?.ContainerId;
                if (!string.IsNullOrWhiteSpace(containerId))
                {
                    _logger.LogInformation("Resolved container {ContainerId} for server {ServerId}", containerId, serverId);
                    return containerId;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve container ID for server {ServerId}", serverId);
            }

            return null;
        }
    }

    /// <summary>
    /// Wire format for attach stream messages sent to clients.
    /// </summary>
    public sealed record AttachStreamMessage(AttachFrameKind Kind, string Payload);
}
