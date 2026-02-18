using Docker.DotNet;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Reflection;
using Scalar.AspNetCore;

namespace GameServer.Docker
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Bootstrap logger - minimal configuration for startup only
            // Don't write to console here to avoid duplicates
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .CreateBootstrapLogger();
                
            try
            {
                //Log startup information
                var asmb = Assembly.GetExecutingAssembly();
                Log.Information($"Starting GameServer.Docker Version - {asmb.GetName().Version}");

                var builder = WebApplication.CreateBuilder(args);

                //Serilog configuration - This replaces the bootstrap logger
                builder.Services.AddSerilog((services, loggerConfig) =>
                    loggerConfig
                        .ReadFrom.Configuration(builder.Configuration)
                        .ReadFrom.Services(services)
                        .Enrich.FromLogContext()
                        .Enrich.WithProperty("ApplicationName", "GameServer.Docker")
                        .WriteTo.Console(
                            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"));

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
                
                // Add SignalR Client for Node Agent connections (log streaming, stats streaming)
                builder.Services.AddSingleton<NodeAgentClient>();
                
                // Add Resource Monitoring (uses Node Agents for real-time stats)
                builder.Services.AddSingleton<IGameServerResourceMonitor, GameServerResourceMonitorService>();

                builder.Services.AddSingleton<PortAllocator>();

                // Add SQLite Database for GameType management
                var connectionString = builder.Configuration.GetConnectionString("GameServerDb") 
                    ?? "Data Source=./data/gameserver.db";//let this default to a sub path to avoid NSwag build errors.
                builder.Services.AddDbContext<Data.GameServerDbContext>(options =>
                    options.UseSqlite(connectionString));

                // Add GameType Repository (database-backed) - This replaces file-based registries
                builder.Services.AddScoped<Repositories.IGameTypeRepository, Repositories.GameTypeRepository>();
                
                // Keep file-based registries as fallback/migration helpers (optional)
                // builder.Services.AddSingleton<IGameTypeRegistry, GaneTypeRegistryFile>();
                // builder.Services.AddSingleton<IGameTypeExtendedMetadataRegistry, GameTypeExtendedMetadataRegistryFile>();
                
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

                // Initialize database on startup
                // The GameTypeRepository.InitializeDatabaseAsync() method:
                // - Creates the database if it doesn't exist
                // - Migrates JSON files only if database is empty
                // Skip initialization if:
                // - Command line argument --no-db-init is present
                // - Environment variable SKIP_DB_INIT is set
                // - Running under NSwag (detected by checking for NSwag in the executing assembly path or command line)
                var entryAssembly = System.Reflection.Assembly.GetEntryAssembly()?.Location ?? "";
                var commandLine = Environment.CommandLine;
                var isNSwagExecution = entryAssembly.Contains("NSwag", StringComparison.OrdinalIgnoreCase) ||
                                       commandLine.Contains("NSwag", StringComparison.OrdinalIgnoreCase);
                var skipDbInit = args.Contains("--no-db-init") || 
                                 Environment.GetEnvironmentVariable("SKIP_DB_INIT") == "true" ||
                                 isNSwagExecution;
                
                if (!skipDbInit)
                {
                    using var scope = app.Services.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<Repositories.IGameTypeRepository>();
                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                    
                    logger.LogInformation("Initializing database...");
                    await repository.InitializeDatabaseAsync();
                    logger.LogInformation("Database initialization complete");
                }
                else
                {
                    var logger = app.Services.GetRequiredService<ILogger<Program>>();
                    logger.LogDebug("Database initialization skipped (NSwag={NSwag}, Entry={Entry})", isNSwagExecution, entryAssembly);
                }

                // Enable CORS (must be before routing)
                app.UseCors();

                app.UseAuthorization();
                
                // Map SignalR hubs
                app.MapHub<Hubs.ContainerConsoleHub>("/hubs/console");
                app.MapHub<Hubs.ServerLogsHub>("/hubs/serverlogs");
                app.MapHub<Hubs.ResourceMonitoringHub>("/hubs/resources");

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
