using GameServer.API.Interfaces;
using GameServer.Orchestration;
using GameServer.Orchestration.Host.Hubs;
using GameServer.Orchestration.Hubs;
using Microsoft.AspNetCore.SignalR;
using Serilog;
using Serilog.Events;
using System.Reflection;

// Bootstrap logger
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .CreateBootstrapLogger();

try
{
    var assembly = Assembly.GetExecutingAssembly();
    var assemblyVersion = assembly.GetName().Version?.ToString() ?? "unknown";
    Log.Information("Starting GameServer.Orchestration.Host v{Version}", assemblyVersion);

    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSerilog((services, cfg) =>
        cfg.ReadFrom.Configuration(builder.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext()
           .Enrich.WithProperty("ApplicationName", "GameServer.Orchestration.Host")
           .Enrich.WithProperty("ApplicationVersion", assemblyVersion));

    // Real SignalR-backed shutdown notifier — broadcasts via AgentRegistrationHub.
    builder.Services.AddSingleton<IAgentShutdownNotifier, SignalRAgentShutdownNotifier>();

    // Orchestration background services (also registers TerminalSessionManager +
    // ITerminalSessionNotifier bound to ContainerConsoleHub).
    builder.Services.AddOrchestrationModule(builder.Configuration);

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
    app.MapHub<AgentRegistrationHub>("/hubs/agentregistration");
    app.MapHub<ContainerConsoleHub>("/hubs/terminal");
    app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

    Log.Information("GameServer.Orchestration.Host started — listening for agent connections.");
    app.Run();
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

// ── Phase 2 SignalR-backed implementations ─────────────────────────────────────

/// <summary>
/// Notifies connected agents that the Orchestration service is shutting down
/// by broadcasting via the AgentRegistrationHub.
/// </summary>
file sealed class SignalRAgentShutdownNotifier(
    IHubContext<AgentRegistrationHub> hubContext) : IAgentShutdownNotifier
{
    public Task NotifyPrimaryServiceShuttingDownAsync(CancellationToken cancellationToken = default)
        => hubContext.Clients.All.SendAsync("PrimaryServiceShuttingDown", cancellationToken);
}
