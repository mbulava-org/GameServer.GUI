namespace GameServer.API.Services.V2;

public sealed class BackupCleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackupCleanupBackgroundService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    public BackupCleanupBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<BackupCleanupBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Backup cleanup background service started (interval: {Interval})", Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var backupService = scope.ServiceProvider.GetRequiredService<IGameServerBackupService>();
                var cleanedCount = await backupService.CleanupExpiredBackupsAsync(stoppingToken);

                if (cleanedCount > 0)
                {
                    _logger.LogInformation("Backup cleanup background service removed {Count} expired backup(s)", cleanedCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during backup cleanup background run");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Backup cleanup background service stopped");
    }
}
