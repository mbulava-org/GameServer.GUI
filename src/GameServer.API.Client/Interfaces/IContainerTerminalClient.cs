using System;
using System.Threading;
using System.Threading.Tasks;

namespace GameServer.API.Client.Interfaces
{
    /// <summary>
    /// SignalR client for an interactive, per-user container exec session.
    /// Each connection gets its own shell process; output is delivered only to
    /// the owning connection. This targets the /hubs/terminal endpoint.
    /// </summary>
    public interface IContainerTerminalClient : IAsyncDisposable
    {
        /// <summary>
        /// Event raised when output is received from the exec session.
        /// </summary>
        event EventHandler<string>? OutputReceived;

        /// <summary>
        /// Event raised when an error occurs.
        /// </summary>
        event EventHandler<string>? ErrorReceived;

        /// <summary>
        /// Event raised when a session has started. Payload is the session ID,
        /// which equals the SignalR connection ID for this hub.
        /// </summary>
        event EventHandler<string>? SessionStarted;

        /// <summary>
        /// Event raised when connected to a container exec session.
        /// </summary>
        event EventHandler<string>? Connected;

        /// <summary>
        /// Event raised when disconnected from the exec session.
        /// </summary>
        event EventHandler<string>? Disconnected;

        /// <summary>
        /// Gets whether the client is currently connected to the hub.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Gets the ID of the currently executing container, or null if not in a session.
        /// </summary>
        string? ContainerId { get; }

        /// <summary>
        /// Gets the connection/session ID of this client, or null when not connected.
        /// </summary>
        string? ConnectionId { get; }

        /// <summary>
        /// Connects to the SignalR hub.
        /// </summary>
        Task ConnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts an interactive exec session for the specified container and shell.
        /// </summary>
        /// <param name="containerId">Container ID to execute the shell in</param>
        /// <param name="shell">Shell command, default /bin/sh</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<bool> StartExecSessionAsync(string containerId, string shell = "/bin/sh", CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends input to the current exec session.
        /// </summary>
        /// <param name="input">Input characters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SendInputAsync(string input, CancellationToken cancellationToken = default);

        /// <summary>
        /// Disconnects from the current exec session.
        /// </summary>
        Task DisconnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the SignalR connection.
        /// </summary>
        Task StopAsync(CancellationToken cancellationToken = default);
    }
}
