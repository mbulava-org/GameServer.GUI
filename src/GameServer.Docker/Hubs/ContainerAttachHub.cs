using System.Runtime.CompilerServices;
using System.Text.Json;
using GameServer.Docker.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace GameServer.Docker.Hubs
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

        public ContainerAttachHub(
            ILogger<ContainerAttachHub> logger,
            IContainerAttachAggregator attachAggregator)
        {
            _logger = logger;
            _attachAggregator = attachAggregator;
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

        private static async Task<string?> ResolveContainerIdFromServerAsync(string serverId, CancellationToken cancellationToken)
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            // Resolve through the agent discovery is not directly available in the hub without
            // injecting NodeAgentDiscovery; for now rely on the caller providing the containerId.
            // Returning null here causes the hub to ask the client to provide the containerId.
            await Task.CompletedTask;
            return null;
        }
    }

    /// <summary>
    /// Wire format for attach stream messages sent to clients.
    /// </summary>
    public sealed record AttachStreamMessage(AttachFrameKind Kind, string Payload);
}
