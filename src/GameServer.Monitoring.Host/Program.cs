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

    // Phase 3: No-op notifiers — terminal sessions and agent shutdown are owned by
    // GameServer.Orchestration.Host and GameServer.API. Monitoring.Host still needs
    // these to satisfy DI for shared consumers even though it no longer co-hosts
    // the orchestration module in-process.
    builder.Services.AddSingleton<IAgentShutdownNotifier, NoOpAgentShutdownNotifier>();
    builder.Services.AddSingleton<ITerminalSessionNotifier, NoOpTerminalSessionNotifier>();

    // Phase 3: Orchestration state is fetched from GameServer.Orchestration.Host over HTTP.
    // The registry + discovery are read-only from this host's perspective; write paths
    // (agent registration, shutdown broadcasts, terminal I/O) live on the Orchestration Host.
    builder.Services.AddOrchestrationHttpClientModule(builder.Configuration);

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

    // Phase 3 hubs — web clients connect here for shared streaming data.
    // Route names intentionally match the aliases exposed by GameServer.API so
    // clients can switch host base URIs without any code changes.
    app.MapHub<ResourceMonitoringHub>("/hubs/resources");
    app.MapHub<ServerLogsHub>("/hubs/serverlogs");
    app.MapHub<ContainerAttachHub>("/hubs/attach");

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
