namespace GameServer.API.Interfaces
{
    /// <summary>
    /// Notifier for agent connection lifecycle events such as Primary Service shutdown.
    /// </summary>
    public interface IAgentShutdownNotifier
    {
        Task NotifyPrimaryServiceShuttingDownAsync(CancellationToken cancellationToken = default);
    }
}
