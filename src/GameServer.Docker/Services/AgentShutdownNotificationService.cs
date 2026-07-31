using GameServer.Docker.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace GameServer.Docker.Services;

/// <summary>
/// Notifies connected agents that the Primary Service is shutting down so they can disconnect gracefully.
/// </summary>
public sealed class AgentShutdownNotificationService(
    IHubContext<AgentRegistrationHub> hubContext,
    ILogger<AgentShutdownNotificationService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Notifying connected agents that the Primary Service is shutting down...");
            await hubContext.Clients.All.SendCoreAsync(
                "PrimaryServiceShuttingDown",
                ["Primary Service is shutting down."],
                cancellationToken);

            // Give the SignalR message a moment to flush to connected agents before the host completes shutdown.
            const int shutdownNotificationGraceMs = 1500;
            try
            {
                await Task.Delay(shutdownNotificationGraceMs, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Host is terminating immediately; we already sent the notification.
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("Agent shutdown notification was cancelled during host shutdown.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to notify agents about Primary Service shutdown.");
        }
    }
}
