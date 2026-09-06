using GameServer.API.Interfaces;
using GameServer.Orchestration;
using GameServer.Orchestration.Host.Hubs;
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

    // Phase 2: Real SignalR-backed notifier — the shutdown hub notifies connected agents.
    // IAgentShutdownNotifier sends via the AgentRegistrationHub context.
    builder.Services.AddSingleton<IAgentShutdownNotifier, SignalRAgentShutdownNotifier>();

    // Phase 2: TerminalSessionNotifier — terminal output forwarded via SignalR.
    builder.Services.AddSingleton<ITerminalSessionNotifier, SignalRTerminalSessionNotifier>();

    // Orchestration background services:
    //   - IAgentRegistry, IUdpAgentRegistry, UdpAnnouncementListenerService
    //   - NodeAgentDiscoveryService, INodeAgentDiscovery
    //   - NodeAgentClient, AgentShutdownNotificationService, TerminalSessionManager
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

/// <summary>
/// Forwards terminal session output through a dedicated terminal SignalR hub.
/// In Phase 2 the terminal hub lives in GameServer.API; this notifier connects back to it.
/// Replace with a direct hub call when the terminal hub moves here in a future phase.
/// </summary>
file sealed class SignalRTerminalSessionNotifier : ITerminalSessionNotifier
{
    // Terminal output is still handled by GameServer.API's ContainerConsoleHub in Phase 2.
    // These methods are no-ops here; the API hub owns the terminal session lifecycle.
    public Task SendOutputAsync(string connectionId, string output, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SendDisconnectedAsync(string connectionId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SendErrorAsync(string connectionId, string error, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
