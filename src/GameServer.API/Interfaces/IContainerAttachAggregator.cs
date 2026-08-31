using System.Runtime.CompilerServices;

namespace GameServer.API.Interfaces
{
    /// <summary>
    /// Shared, multi-subscriber container attach stream aggregator.
    /// Keeps one underlying agent attach WebSocket per container ID and fans output frames
    /// out to all connected SignalR clients. One subscriber at a time may have input control.
    /// </summary>
    public interface IContainerAttachAggregator
    {
        /// <summary>
        /// Subscribe to a shared attach stream for the given container.
        /// </summary>
        /// <param name="connectionId">SignalR caller connection ID</param>
        /// <param name="containerId">Container ID to attach to</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Async enumerable of output/control frames</returns>
        IAsyncEnumerable<AttachStreamFrame> SubscribeAsync(
            string connectionId,
            string containerId,
            [EnumeratorCancellation] CancellationToken cancellationToken = default);

        /// <summary>
        /// Send input into the shared attach stream. Only succeeds if the caller is the
        /// current input controller for the container.
        /// </summary>
        /// <param name="connectionId">SignalR caller connection ID</param>
        /// <param name="containerId">Container ID</param>
        /// <param name="input">Input to send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the input was sent; otherwise false</returns>
        Task<bool> SendInputAsync(
            string connectionId,
            string containerId,
            string input,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Unsubscribe a connection from the shared attach stream. If the connection was the
        /// input controller, control is released and remaining subscribers are notified.
        /// </summary>
        /// <param name="connectionId">SignalR caller connection ID</param>
        /// <param name="containerId">Container ID</param>
        Task UnsubscribeAsync(string connectionId, string containerId);
    }

    /// <summary>
    /// A single frame emitted by the shared attach aggregator.
    /// </summary>
    public sealed class AttachStreamFrame
    {
        /// <summary>
        /// Frame kind.
        /// </summary>
        public required AttachFrameKind Kind { get; init; }

        /// <summary>
        /// Text payload. Meaning depends on <see cref="Kind"/>:
        /// Output => stdout/stderr text;
        /// InputControlledBy => connection ID of the controller;
        /// Error => error message.
        /// </summary>
        public string Payload { get; init; } = string.Empty;
    }

    public enum AttachFrameKind
    {
        Output,
        InputControlledBy,
        Error
    }
}
