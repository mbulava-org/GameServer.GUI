using GameServer.API.Interfaces;
using GameServer.Catalog;
using GameServer.Monitoring;
using GameServer.Monitoring.Host.Hubs;
using GameServer.Orchestration;
using Serilog;
using Serilog.Events;
using System.Reflection;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .CreateBootstrapLogger();

try
{
    var assembly = Assembly.GetExecutingAssembly();
    var assemblyVersion = assembly.GetName().Version?.ToString() ?? "unknown";
    Log.Information("Starting GameServer.Monitoring.Host v{Version}", assemblyVersion);

    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSerilog((services, cfg) =>
        cfg.ReadFrom.Configuration(builder.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext()
           .Enrich.WithProperty("ApplicationName", "GameServer.Monitoring.Host")
           .Enrich.WithProperty("ApplicationVersion", assemblyVersion));

    // Phase 2: No-op notifiers — terminal sessions and agent shutdown are owned by
    // GameServer.Orchestration.Host. Monitoring Host only needs these to satisfy
    // Orchestration module's DI requirements (it co-hosts Orchestration in Phase 2).
    builder.Services.AddSingleton<IAgentShutdownNotifier, NoOpAgentShutdownNotifier>();
    builder.Services.AddSingleton<ITerminalSessionNotifier, NoOpTerminalSessionNotifier>();

    // Phase 2: Orchestration is still co-hosted here so IAgentRegistry and INodeAgentDiscovery
    // are available in-process for the Monitoring aggregators.
    // Phase 3 (future): Replace with typed HTTP clients pointing at Orchestration.Host.
    builder.Services.AddOrchestrationModule(builder.Configuration);

    // Catalog (repositories used by GameServerQueryService + GameTypeQueryService).
    // skipDbInit: true — database schema migrations are owned by GameServer.API.
    builder.Services.AddCatalogModule(builder.Configuration, skipDbInit: true);

    // Monitoring services: aggregators, resource collector, readiness watcher, query services.
    builder.Services.AddMonitoringModule();

    builder.Services.AddControllers();
    builder.Services.AddSignalR();

    builder.Services.AddCors(options =>
        options.AddDefaultPolicy(policy =>
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()));

    var app = builder.Build();

    app.UseSerilogRequestLogging(opts =>
    {
        opts.GetLevel = (ctx, _, ex) =>
            ex != null || ctx.Response.StatusCode >= 500 ? LogEventLevel.Error :
            ctx.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Verbose :
            LogEventLevel.Information;
    });

    app.UseCors();
    app.UseAuthorization();
    app.MapControllers();

    // Phase 2 hubs — web clients now connect here for all streaming data
    app.MapHub<ResourceMonitoringHub>("/hubs/resourcemonitoring");
    app.MapHub<ServerLogsHub>("/hubs/serverlogs");
    app.MapHub<ContainerAttachHub>("/hubs/containerattach");

    app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

    Log.Information("GameServer.Monitoring.Host started — streaming hubs active.");
    app.Run();
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

// ── Phase 2 no-op stubs ────────────────────────────────────────────────────────

file sealed class NoOpAgentShutdownNotifier : IAgentShutdownNotifier
{
    public Task NotifyPrimaryServiceShuttingDownAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

file sealed class NoOpTerminalSessionNotifier : ITerminalSessionNotifier
{
    public Task SendOutputAsync(string connectionId, string output, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SendDisconnectedAsync(string connectionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SendErrorAsync(string connectionId, string error, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
