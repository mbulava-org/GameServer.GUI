using V2Repositories = GameServer.Docker.Repositories.V2;

namespace GameServer.Docker.Services
{
    /// <summary>
    /// Background service that initializes the V2 database after the webhost has started.
    /// This allows the webhost and SignalR hubs to be available immediately while
    /// database initialization happens in the background.
    /// </summary>
    public class DatabaseInitializationService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly IDatabaseReadinessGate _readinessGate;
        private readonly ILogger<DatabaseInitializationService> _logger;

        public DatabaseInitializationService(
            IServiceProvider serviceProvider,
            IHostApplicationLifetime applicationLifetime,
            IDatabaseReadinessGate readinessGate,
            ILogger<DatabaseInitializationService> logger)
        {
            _serviceProvider = serviceProvider;
            _applicationLifetime = applicationLifetime;
            _readinessGate = readinessGate;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Small delay to ensure webhost is fully started
            await Task.Delay(100, stoppingToken);

            _logger.LogInformation("🔄 Starting background database initialization...");

            try
            {
                // Create a scope to resolve scoped services (DbContext is scoped)
                using var scope = _serviceProvider.CreateScope();
                var v2Repository = scope.ServiceProvider.GetRequiredService<V2Repositories.IGameTypeRepository>();

                await v2Repository.InitializeDatabaseAsync();

                _readinessGate.MarkReady();
                _logger.LogInformation("Background database initialization complete for V2 persistence store.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Database initialization was cancelled during host shutdown.");
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 1;
                _logger.LogCritical(ex, "❌ Failed to initialize database in background. Shutting down the application.");
                _applicationLifetime.StopApplication();
            }
        }
    }
}
