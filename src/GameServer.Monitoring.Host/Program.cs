using GameServer.API.Interfaces;
using GameServer.Catalog;
using GameServer.Monitoring;
using GameServer.Orchestration;
using Serilog;
using System.Reflection;

// Bootstrap logger for startup errors
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .CreateBootstrapLogger();

try
{
    var assembly = Assembly.GetExecutingAssembly();
    var assemblyVersion = assembly.GetName().Version?.ToString() ?? "unknown";
    Log.Information("Starting GameServer.Monitoring.Host v{Version}", assemblyVersion);

    var builder = Host.CreateApplicationBuilder(args);

    // Serilog — reads from appsettings.json
    builder.Services.AddSerilog((services, cfg) =>
        cfg.ReadFrom.Configuration(builder.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext()
           .Enrich.WithProperty("ApplicationName", "GameServer.Monitoring.Host")
           .Enrich.WithProperty("ApplicationVersion", assemblyVersion));

    // Phase 1: Orchestration services are co-hosted here so Monitoring can resolve
    // IAgentRegistry and INodeAgentDiscovery in-process.
    // Phase 2: These will be replaced with HTTP clients pointing at the standalone
    // GameServer.Orchestration.Host container.

    // No-op stubs for SignalR-backed notifiers (API owns the hubs in Phase 1)
    builder.Services.AddSingleton<IAgentShutdownNotifier, NoOpAgentShutdownNotifier>();
    builder.Services.AddSingleton<ITerminalSessionNotifier, NoOpTerminalSessionNotifier>();

    // Register Orchestration module (provides IAgentRegistry, INodeAgentDiscovery, etc.)
    builder.Services.AddOrchestrationModule(builder.Configuration);

    // Register Catalog module (provides repositories used by Monitoring query services)
    // skipDbInit: true — database initialization is owned by GameServer.API.
    builder.Services.AddCatalogModule(builder.Configuration, skipDbInit: true);

    // Register all monitoring background services:
    //   - GameServerQueryService, GameTypeQueryService
    //   - IServerResourceMonitor, IServerResourceAggregator, IGameServerResourceCollector (hosted)
    //   - IServerLogAggregator
    //   - IContainerAttachAggregator
    //   - IGameServerReadinessWatcherService
    builder.Services.AddMonitoringModule();

    var host = builder.Build();

    Log.Information("GameServer.Monitoring.Host built successfully — starting background services.");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "GameServer.Monitoring.Host terminated unexpectedly");
    Environment.ExitCode = 1;
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// ── Phase 1 no-op stubs ───────────────────────────────────────────────────────

file sealed class NoOpAgentShutdownNotifier : IAgentShutdownNotifier
{
    public Task NotifyPrimaryServiceShuttingDownAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

file sealed class NoOpTerminalSessionNotifier : ITerminalSessionNotifier
{
    public Task SendOutputAsync(string connectionId, string output, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SendDisconnectedAsync(string connectionId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SendErrorAsync(string connectionId, string error, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
