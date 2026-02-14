using Docker.DotNet;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Services;
using Serilog;
using System.Reflection;
using Scalar.AspNetCore;

namespace GameServer.Docker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateBootstrapLogger();
            try
            {
                //Log startup information
                var asmb = Assembly.GetCallingAssembly();
                Log.Information($"Starting GameServer.Docker Version - {asmb.GetName().Version}");

                var builder = WebApplication.CreateBuilder(args);

                //Serilog configuration
                builder.Services.AddSerilog((services, loggerConfig) =>
                    loggerConfig
                        .ReadFrom.Configuration(builder.Configuration)
                        .ReadFrom.Services(services)
                        .Enrich.FromLogContext()
                        .Enrich.WithProperty("ApplicationName", "GameServer.Docker")
                        .WriteTo.Console());

                //Add configuration sources
                builder.Services.Configure<Configurations.DockerConnection>(builder.Configuration.GetSection("DockerConnection"));
                builder.Services.Configure<Configurations.PortAllocation>(builder.Configuration.GetSection("PortAllocation"));
                builder.Services.Configure<Configurations.GameTypeRegistryData>(builder.Configuration.GetSection("GameTypeRegistryData"));
                builder.Services.Configure<Configurations.GameTypeExtendedMetadataRegistryData>(builder.Configuration.GetSection("GameTypeExtendedMetadataRegistryData"));
                builder.Services.Configure<Configurations.VolumeDriverConfigOptions>(builder.Configuration.GetSection("VolumeDriverConfigOptions"));
                builder.Services.Configure<Configurations.NetworkOptions>(builder.Configuration.GetSection("NetworkOptions"));
                builder.Services.Configure<Configurations.NodeAgentOptions>(builder.Configuration.GetSection("NodeAgentOptions"));

                // Add services to the container.
                builder.Services.AddSingleton<DockerClientFactory>();   
                builder.Services.AddSingleton<IDockerClient>(sp =>
                {
                    var dockerClientFactory = sp.GetRequiredService<DockerClientFactory>();
                    return dockerClientFactory.Create();
                });

                builder.Services.AddSingleton<DockerServiceHelper>();

                // File Management
                builder.Services.AddSingleton<IGameServerFileManager, GameServerFileManagerService>();

                // Server Management
                builder.Services.AddSingleton<IGameServerManager, GameServerManagerService>();
                
                // Node Agent Discovery (for real-time container stats)
                // Registered as both singleton and hosted service for background discovery
                // HttpClient instances are created per node agent for optimal connection pooling
                // Timeout is configured per-client via NodeAgentOptions in the service
                builder.Services.AddHttpClient(); // Default factory for creating per-node clients
                builder.Services.AddSingleton<NodeAgentDiscoveryService>();
                builder.Services.AddSingleton<INodeAgentDiscovery>(sp => sp.GetRequiredService<NodeAgentDiscoveryService>());
                builder.Services.AddHostedService(sp => sp.GetRequiredService<NodeAgentDiscoveryService>());
                
                // Add Resource Monitoring (uses Node Agents for real-time stats)
                builder.Services.AddSingleton<IGameServerResourceMonitor, GameServerResourceMonitorService>();

                builder.Services.AddSingleton<PortAllocator>();

                builder.Services.AddSingleton<IGameTypeRegistry, GaneTypeRegistryFile>();
                builder.Services.AddSingleton<IGameTypeExtendedMetadataRegistry, GameTypeExtendedMetadataRegistryFile>();
                builder.Services.AddSingleton<GameTypeMetadataApplier>();
                builder.Services.AddScoped<ServerLifecycleService>();

                builder.Services.AddControllers();
                
                // Add SignalR for real-time features (console, logs, monitoring)
                builder.Services.AddSignalR();
                
                // Add CORS for Blazor frontend (required for SignalR WebSockets)
                builder.Services.AddCors(options =>
                {
                    options.AddDefaultPolicy(policy =>
                    {
                        policy.SetIsOriginAllowed(origin => true) // Allow any origin in development
                              .AllowAnyHeader()
                              .AllowAnyMethod()
                              .AllowCredentials(); // Required for SignalR!
                    });
                });
                
                // Use built-in OpenAPI for .NET 10 (required for NSwag client generation)
                builder.Services.AddOpenApi(options =>
                {
                    options.AddDocumentTransformer((document, context, cancellationToken) =>
                    {
                        document.Info.Title = "GameServer.Docker API";
                        document.Info.Version = "v1";
                        document.Info.Description = "GameServer.Docker ASP.NET Core Web API";
                        return Task.CompletedTask;
                    });
                });

                // Add NSwag document generator services (required for client code generation)
                // Note: We only add the generator services, not the middleware
                builder.Services.AddOpenApiDocument(opts =>
                {
                    opts.Title = "GameServer.Docker API";
                    opts.Version = "v1";
                    opts.Description = "GameServer.Docker ASP.NET Core Web API";
                });

                
                var app = builder.Build();

                // Add Serilog request logging
                app.UseSerilogRequestLogging();

                // Map OpenAPI endpoint and Scalar UI
                app.MapOpenApi();
                app.MapScalarApiReference(options =>
                {
                    options.WithTitle("GameServer.Docker API");
                    options.WithTheme(ScalarTheme.BluePlanet);
                    options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
                });

                app.UseHttpsRedirection();

                // Enable CORS (must be before routing)
                app.UseCors();

                app.UseAuthorization();
                
                // Map SignalR hubs
                app.MapHub<GameServer.Docker.Hubs.ContainerConsoleHub>("/hubs/console");
                app.MapHub<GameServer.Docker.Hubs.ResourceMonitoringHub>("/hubs/resources");

                app.MapControllers();

                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
