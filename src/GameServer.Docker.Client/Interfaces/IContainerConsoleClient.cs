using System;
using System.Threading;
using System.Threading.Tasks;

namespace GameServer.Docker.Client.Interfaces
{
    /// <summary>
    /// SignalR client for a shared, multi-subscriber container attach stream.
    /// Multiple viewers of the same container see the same output. The first
    /// subscriber to send input becomes the controller; others are notified.
    /// For an interactive per-user exec shell, use <see cref="IContainerTerminalClient"/>.
    /// </summary>
    public interface IContainerConsoleClient : IAsyncDisposable
    {
        /// <summary>
        /// Event raised when output is received from the shared attach stream.
        /// </summary>
        event EventHandler<string>? OutputReceived;

        /// <summary>
        /// Event raised when an error occurs.
        /// </summary>
        event EventHandler<string>? ErrorReceived;

        /// <summary>
        /// Event raised when attached to a container.
        /// </summary>
        event EventHandler<string>? Connected;

        /// <summary>
        /// Event raised when disconnected from a container.
        /// </summary>
        event EventHandler<string>? Disconnected;

        /// <summary>
        /// Event raised when input control changes. The payload is the connection ID
        /// of the controller, or empty when control is released.
        /// </summary>
        event EventHandler<string>? InputControlChanged;

        /// <summary>
        /// Gets whether the client is currently connected to the hub.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Gets the ID of the currently attached container, or null if not attached.
        /// </summary>
        string? AttachedContainerId { get; }

        /// <summary>
        /// Gets the connection ID of this client, or null when not connected.
        /// </summary>
        string? ConnectionId { get; }

        /// <summary>
        /// Connects to the SignalR hub.
        /// </summary>
        Task ConnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Attaches to a container's shared console stream.
        /// </summary>
        /// <param name="serverId">Game server ID used to resolve the container when no containerId is supplied</param>
        /// <param name="containerId">Optional explicit container ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task AttachToContainerAsync(string serverId, string? containerId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends input to the shared attach stream. The caller must be the current
        /// controller; otherwise the server ignores the input.
        /// </summary>
        Task SendInputAsync(string input, CancellationToken cancellationToken = default);

        /// <summary>
        /// Disconnects from the shared attach stream.
        /// </summary>
        Task DisconnectFromContainerAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the SignalR connection.
        /// </summary>
        Task StopAsync(CancellationToken cancellationToken = default);
    }
}
