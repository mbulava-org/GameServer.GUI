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
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateBootstrapLogger();
            try
            {
                //Log startup information
                var asmb = Assembly.GetExecutingAssembly();
                Log.Information($"Starting GameServer.Docker Version - {asmb.GetName().Version}");

                var builder = WebApplication.CreateBuilder(args);

                //Serilog configuration
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
                
                // Add Resource Monitoring (uses Node Agents for real-time stats)
                builder.Services.AddSingleton<IGameServerResourceMonitor, GameServerResourceMonitorService>();

                builder.Services.AddSingleton<PortAllocator>();

                // Add SQLite Database for GameType management
                var connectionString = builder.Configuration.GetConnectionString("GameServerDb") 
                    ?? "Data Source=./data/gameserver.db";
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

                // Initialize database
                //await InitializeDatabaseAsync(app.Services);

                // Enable CORS (must be before routing)
                app.UseCors();

                app.UseAuthorization();
                
                // Map SignalR hubs
                app.MapHub<Hubs.ContainerConsoleHub>("/hubs/console");
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

        /// <summary>
        /// Initialize database on startup
        /// </summary>
        //private static async Task InitializeDatabaseAsync(IServiceProvider services)
        //{
        //    using var scope = services.CreateScope();
        //    var context = scope.ServiceProvider.GetRequiredService<Data.GameServerDbContext>();
        //    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        //    try
        //    {
        //        // Ensure database is created
        //        logger.LogInformation("Initializing database...");
        //        await context.Database.EnsureCreatedAsync();
                
        //        // Check if we need to migrate from JSON files
        //        var gameTypesCount = await context.GameTypes.CountAsync();
        //        if (gameTypesCount == 0)
        //        {
        //            logger.LogInformation("Database is empty. Checking for existing JSON files to migrate...");
        //            await MigrateFromJsonIfExistsAsync(scope.ServiceProvider, logger);
        //        }
        //        else
        //        {
        //            logger.LogInformation("Database initialized. Found {Count} game types.", gameTypesCount);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.LogError(ex, "Error initializing database");
        //        throw;
        //    }
        //}

        /// <summary>
        /// Migrate data from existing JSON files if they exist
        /// </summary>
        //private static async Task MigrateFromJsonIfExistsAsync(IServiceProvider services, Microsoft.Extensions.Logging.ILogger logger)
        //{
        //    var configuration = services.GetRequiredService<IConfiguration>();
        //    var dataDirectory = configuration.GetValue<string>("GameTypeStorage:DataDirectory") ?? "./data";
        //    var gameTypesDir = Path.Combine(dataDirectory, "gametypes");

        //    if (!Directory.Exists(gameTypesDir))
        //    {
        //        logger.LogInformation("No JSON files found to migrate.");
        //        return;
        //    }

        //    var jsonFiles = Directory.GetFiles(gameTypesDir, "*.json");
        //    if (jsonFiles.Length == 0)
        //    {
        //        logger.LogInformation("No JSON files found to migrate.");
        //        return;
        //    }

        //    logger.LogInformation("Found {Count} JSON files to migrate.", jsonFiles.Length);

        //    var context = services.GetRequiredService<Data.GameServerDbContext>();
        //    var migrated = 0;

        //    foreach (var file in jsonFiles)
        //    {
        //        try
        //        {
        //            var json = await File.ReadAllTextAsync(file);
        //            var gameType = System.Text.Json.JsonSerializer.Deserialize<Models.GameTypeDefinition>(json);

        //            if (gameType != null && !await context.GameTypes.AnyAsync(gt => gt.Key == gameType.Key))
        //            {
        //                var entity = new Data.GameTypeEntity
        //                {
        //                    Key = gameType.Key,
        //                    DisplayName = gameType.DisplayName,
        //                    Description = gameType.Description,
        //                    Image = gameType.Image,
        //                    ThumbnailUrl = gameType.ThumbnailUrl,
        //                    DocumentationUrl = gameType.DocumentationUrl,
        //                    IsActive = true,
        //                    Ports = gameType.Ports?.Select(p => new Data.PortEntity
        //                    {
        //                        Port = (int)p.Port,
        //                        Protocol = p.Protocol,
        //                        IsDefaultPort = p.IsDefaultPort
        //                    }).ToList() ?? new List<Data.PortEntity>(),
        //                    Volumes = gameType.Volumes?.Select(v => new Data.VolumeEntity
        //                    {
        //                        Source = v.Source,
        //                        Target = v.Target,
        //                        ReadOnly = false  // Default to false, model doesn't have this property yet
        //                    }).ToList() ?? new List<Data.VolumeEntity>(),
        //                    DefaultSettings = gameType.DefaultSettings?.Select(ds => new Data.DefaultSettingEntity
        //                    {
        //                        SettingKey = ds.Key,
        //                        SettingValue = ds.Value
        //                    }).ToList() ?? new List<Data.DefaultSettingEntity>()
        //                };

        //                context.GameTypes.Add(entity);
        //                migrated++;
        //                logger.LogInformation("Migrated GameType: {Key}", gameType.Key);
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            logger.LogWarning(ex, "Error migrating file: {File}", file);
        //        }
        //    }

        //    if (migrated > 0)
        //    {
        //        await context.SaveChangesAsync();
        //        logger.LogInformation("Migration complete. Migrated {Count} game types from JSON to database.", migrated);
        //    }
        //}
    }
}
