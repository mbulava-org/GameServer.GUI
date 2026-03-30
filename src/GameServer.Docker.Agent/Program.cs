using Docker.DotNet;
using GameServer.Docker.Agent.Interfaces;
using GameServer.Docker.Agent.Services;
using GameServer.Docker.Agent.Configurations;
using Serilog;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog with environment-based log level
var logLevelEnv = Environment.GetEnvironmentVariable("LOG_LEVEL") ?? "Information";
var logLevel = Enum.TryParse<Serilog.Events.LogEventLevel>(logLevelEnv, true, out var parsedLevel) 
    ? parsedLevel 
    : Serilog.Events.LogEventLevel.Information;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Is(logLevel)  // Configurable via LOG_LEVEL env var (default: Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning) // Suppress ASP.NET Core Info logs
    .MinimumLevel.Override("Microsoft.AspNetCore.SignalR", Serilog.Events.LogEventLevel.Error) // Suppress SignalR connection logs
    .MinimumLevel.Override("Microsoft.AspNetCore.Http.Connections", Serilog.Events.LogEventLevel.Error) // Suppress WebSocket logs
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ApplicationName", "GameServer.Docker.Agent")
    .Enrich.WithProperty("NodeName", Environment.GetEnvironmentVariable("NODE_NAME") ?? "unknown")
    .WriteTo.Console()
    .CreateLogger();

//Log startup information
var asmb = Assembly.GetExecutingAssembly();
Log.Information($"Starting GameServer.Docker.Agent Version - {asmb.GetName().Version}");

builder.Host.UseSerilog();

// Docker client with read-only access to local socket
builder.Services.AddSingleton<IDockerClient>(sp =>
{
    var dockerUri = Environment.GetEnvironmentVariable("DOCKER_HOST") ?? "unix:///var/run/docker.sock";
    var logger = sp.GetRequiredService<ILogger<Program>>();
    
    logger.LogInformation("Connecting to Docker daemon at {DockerUri}", dockerUri);
    
    try
    {
        var config = new DockerClientConfiguration(new Uri(dockerUri));
        var client = config.CreateClient();
        
        // Test connection on startup
        try
        {
            var version = client.System.GetVersionAsync().GetAwaiter().GetResult();
            logger.LogInformation("Successfully connected to Docker daemon. Version: {Version}, API Version: {ApiVersion}", 
                version.Version, version.APIVersion);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to Docker daemon at {DockerUri}. " +
                "Ensure the Docker socket is mounted and accessible. " +
                "The container may need to run as root or have docker group permissions.", dockerUri);
            throw;
        }
        
        return client;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to create Docker client for {DockerUri}", dockerUri);
        throw;
    }
});

builder.Services.Configure<ContainerStatsStreamOptions>(builder.Configuration.GetSection("ContainerStatsStreamOptions"));

// Configure Agent Registration options
builder.Services.Configure<AgentRegistrationOptions>(builder.Configuration.GetSection("AgentRegistration"));
builder.Services.Configure<UdpAgentAnnouncementOptions>(builder.Configuration.GetSection(UdpAgentAnnouncementOptions.SectionName));

// Register services
builder.Services.AddSingleton<IContainerService, ContainerService>();

// Register Agent Registration background service (new architecture)
// This service connects to the Primary Service and pushes agent state
builder.Services.AddHostedService<AgentRegistrationService>();
builder.Services.AddHostedService<UdpAgentAnnouncementService>();

// Add SignalR for real-time communication with Primary Service
builder.Services.AddSignalR();

// Add controllers
builder.Services.AddControllers();

var app = builder.Build();

// Configure middleware
app.UseSerilogRequestLogging();

// Enable WebSockets for container console attach
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

// Map SignalR hub for Primary Service to connect to
app.MapHub<GameServer.Docker.Agent.Hubs.NodeAgentHub>("/hubs/nodeagent");

app.MapControllers();

Log.Information("Node Agent starting on {NodeName}...", Environment.GetEnvironmentVariable("NODE_NAME") ?? "unknown");

app.Run();

