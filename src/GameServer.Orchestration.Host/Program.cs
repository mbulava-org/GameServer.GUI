using GameServer.API.Interfaces;
using GameServer.Orchestration;
using Serilog;
using Serilog.Events;
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
    Log.Information("Starting GameServer.Orchestration.Host v{Version}", assemblyVersion);

    var builder = Host.CreateApplicationBuilder(args);

    // Serilog — reads from appsettings.json
    builder.Services.AddSerilog((services, cfg) =>
        cfg.ReadFrom.Configuration(builder.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext()
           .Enrich.WithProperty("ApplicationName", "GameServer.Orchestration.Host")
           .Enrich.WithProperty("ApplicationVersion", assemblyVersion));

    // Register a no-op IAgentShutdownNotifier — in Phase 1 the Orchestration Host has no
    // SignalR hub. Phase 2 will replace this with a real hub-backed implementation.
    builder.Services.AddSingleton<IAgentShutdownNotifier, NoOpAgentShutdownNotifier>();

    // Register a no-op ITerminalSessionNotifier — terminal session routing is handled
    // by GameServer.API's SignalR hubs. Phase 2 will wire this up properly.
    builder.Services.AddSingleton<ITerminalSessionNotifier, NoOpTerminalSessionNotifier>();

    // Register all orchestration background services:
    //   - IAgentRegistry (AgentRegistryService)
    //   - IUdpAgentRegistry (UdpAgentRegistryService)
    //   - UdpAgentAnnouncementListenerService (hosted)
    //   - NodeAgentDiscoveryService (hosted)
    //   - INodeAgentDiscovery
    //   - NodeAgentClient
    //   - AgentShutdownNotificationService (hosted)
    //   - TerminalSessionManager
    builder.Services.AddOrchestrationModule(builder.Configuration);

    var host = builder.Build();

    Log.Information("GameServer.Orchestration.Host built successfully — starting background services.");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "GameServer.Orchestration.Host terminated unexpectedly");
    Environment.ExitCode = 1;
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// ── Phase 1 no-op stubs ───────────────────────────────────────────────────────
// These are replaced in Phase 2 when the Orchestration service exposes its own
// SignalR hub and the API switches to HTTP clients.

/// <summary>
/// No-op implementation of IAgentShutdownNotifier for Phase 1.
/// The Orchestration Host does not have a SignalR hub yet; shutdown notifications
/// are sent by GameServer.API which still runs its own SignalR hub.
/// </summary>
file sealed class NoOpAgentShutdownNotifier : IAgentShutdownNotifier
{
    public Task NotifyPrimaryServiceShuttingDownAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

/// <summary>
/// No-op implementation of ITerminalSessionNotifier for Phase 1.
/// Terminal session output is forwarded by GameServer.API's SignalR hub.
/// </summary>
file sealed class NoOpTerminalSessionNotifier : ITerminalSessionNotifier
{
    public Task SendOutputAsync(string connectionId, string output, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SendDisconnectedAsync(string connectionId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SendErrorAsync(string connectionId, string error, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
