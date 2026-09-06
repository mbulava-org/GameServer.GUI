namespace GameServer.API.Interfaces
{
    /// <summary>
    /// Notifier for real-time terminal session events (SignalR output, disconnection, error).
    /// </summary>
    public interface ITerminalSessionNotifier
    {
        Task SendOutputAsync(string connectionId, string output, CancellationToken cancellationToken = default);
        Task SendDisconnectedAsync(string connectionId, CancellationToken cancellationToken = default);
        Task SendErrorAsync(string connectionId, string error, CancellationToken cancellationToken = default);
    }
}
