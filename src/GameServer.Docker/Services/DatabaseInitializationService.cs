using GameServer.Docker.Repositories;

namespace GameServer.Docker.Services
{
    /// <summary>
    /// Background service that initializes the database after the webhost has started.
    /// This allows the webhost and SignalR hubs to be available immediately while
    /// database initialization happens in the background.
    /// </summary>
    public class DatabaseInitializationService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DatabaseInitializationService> _logger;

        public DatabaseInitializationService(
            IServiceProvider serviceProvider,
            ILogger<DatabaseInitializationService> logger)
        {
            _serviceProvider = serviceProvider;
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
                var repository = scope.ServiceProvider.GetRequiredService<IGameTypeRepository>();

                await repository.InitializeDatabaseAsync();

                _logger.LogInformation("✅ Background database initialization complete");
            }
            catch (Exception ex)
            {
                // Log the error but don't crash the application
                // The application can still serve API requests, just without database data
                _logger.LogError(ex, "❌ Failed to initialize database in background. Some features may not work correctly.");
                
                // Optionally, you could set a flag here to indicate database is not ready
                // and return appropriate errors from API endpoints that need the database
            }
        }
    }
}
