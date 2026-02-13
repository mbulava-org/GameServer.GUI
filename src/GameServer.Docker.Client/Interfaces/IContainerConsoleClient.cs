using System;
using System.Threading;
using System.Threading.Tasks;

namespace GameServer.Docker.Client.Interfaces
{
    /// <summary>
    /// Interface for real-time container console operations via SignalR.
    /// Provides bidirectional communication with container consoles.
    /// </summary>
    public interface IContainerConsoleClient : IAsyncDisposable
    {
        /// <summary>
        /// Event raised when output is received from the container
        /// </summary>
        event EventHandler<string>? OutputReceived;

        /// <summary>
        /// Event raised when an error occurs
        /// </summary>
        event EventHandler<string>? ErrorReceived;

        /// <summary>
        /// Event raised when connected to a container
        /// </summary>
        event EventHandler<string>? Connected;

        /// <summary>
        /// Event raised when disconnected from a container
        /// </summary>
        event EventHandler<string>? Disconnected;

        /// <summary>
        /// Event raised when command output is received
        /// </summary>
        event EventHandler<string>? CommandOutputReceived;

        /// <summary>
        /// Gets whether the client is currently connected to the hub
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Gets the ID of the currently attached container, or null if not attached
        /// </summary>
        string? AttachedContainerId { get; }

        /// <summary>
        /// Connects to the SignalR hub
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the connection operation</returns>
        Task ConnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Attaches to a container's console
        /// </summary>
        /// <param name="containerId">Container ID to attach to</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if successfully attached, false otherwise</returns>
        Task<bool> AttachToContainerAsync(string containerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends input to the attached container's stdin
        /// </summary>
        /// <param name="input">Input text to send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the send operation</returns>
        Task SendInputAsync(string input, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a command in the container and returns the output
        /// </summary>
        /// <param name="containerId">Container ID</param>
        /// <param name="command">Command to execute</param>
        /// <param name="args">Command arguments</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Command output</returns>
        Task<string> ExecCommandAsync(string containerId, string command, string[]? args = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts an interactive exec session via WebSocket (direct to Agent).
        /// Similar to AttachToContainer but runs a specific command instead of attaching to main process.
        /// Use this for interactive shells or commands that need stdin (e.g., bash, vim, top).
        /// </summary>
        /// <param name="agentUrl">Agent URL (e.g., "http://agent:8080")</param>
        /// <param name="containerId">Container ID</param>
        /// <param name="command">Command to execute (e.g., "bash", "sh")</param>
        /// <param name="args">Command arguments (e.g., ["-i"] for interactive bash)</param>
        /// <param name="tty">Enable TTY mode for proper terminal emulation</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the exec session (completes when session ends)</returns>
        Task ExecInteractiveAsync(string agentUrl, string containerId, string command, string[]? args = null, bool tty = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Disconnects from the current container
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the disconnect operation</returns>
        Task DisconnectFromContainerAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the SignalR connection
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the stop operation</returns>
        Task StopAsync(CancellationToken cancellationToken = default);
    }
}
