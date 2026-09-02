using System.Reflection;
using GameServer.Windows.Agent.Configurations;
using GameServer.Windows.Agent.Hubs;
using GameServer.Windows.Agent.Interfaces;
using GameServer.Windows.Agent.Services;
using Scalar.AspNetCore;
using Serilog;

var assembly = Assembly.GetExecutingAssembly();
var assemblyVersion = assembly.GetName().Version?.ToString() ?? "unknown";
var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? assemblyVersion;

var builder = WebApplication.CreateBuilder(args);

// Support running as a Windows Service when installed
builder.Host.UseWindowsService();

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.SignalR", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ApplicationName", "GameServer.Windows.Agent")
    .Enrich.WithProperty("ApplicationVersion", assemblyVersion)
    .Enrich.WithProperty("NodeName", Environment.MachineName)
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

Log.Information("Starting GameServer.Windows.Agent Version {AssemblyVersion} on {MachineName}",
    assemblyVersion, Environment.MachineName);

// Configuration options
builder.Services.Configure<WindowsAgentOptions>(builder.Configuration.GetSection(WindowsAgentOptions.SectionName));

// Core services
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ISteamCmdService, SteamCmdService>();
builder.Services.AddSingleton<IGameProcessManager, GameProcessManager>();
builder.Services.AddSingleton<IWindowsPortService, WindowsPortService>();
builder.Services.AddSingleton<IWindowsResourceMonitor, WindowsResourceMonitor>();
builder.Services.AddSingleton<IFileManagerService, FileManagerService>();

// Agent registration client
builder.Services.AddHostedService<AgentRegistrationService>();

// SignalR for real-time streaming
builder.Services.AddSignalR();

// Controllers
builder.Services.AddControllers();

// OpenAPI & Scalar
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = "GameServer.Windows.Agent API";
        document.Info.Version = "v1";
        document.Info.Description = "Windows Host Agent API for SteamCMD and Game Process Management";
        return Task.CompletedTask;
    });
});

var app = builder.Build();

app.UseSerilogRequestLogging();

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.WithTitle("GameServer.Windows.Agent API");
    options.WithTheme(ScalarTheme.BluePlanet);
});

// Map SignalR Hub
app.MapHub<WindowsAgentHub>("/hubs/windowsagent");
// Alias to /hubs/nodeagent for compatibility with common node agent client
app.MapHub<WindowsAgentHub>("/hubs/nodeagent");

app.MapControllers();

Log.Information("GameServer.Windows.Agent initialized and ready.");

app.Run();
