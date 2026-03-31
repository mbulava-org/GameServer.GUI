using Docker.DotNet;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using System.Reflection;
using Scalar.AspNetCore;
using RepositoriesV2 = GameServer.Docker.Repositories.V2;
using DataV2 = GameServer.Docker.Data.V2;
using ServicesV2 = GameServer.Docker.Services.V2;
using ServicesV2Detection = GameServer.Docker.Services.V2.Detection;

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

                // Clear default logging providers to prevent duplicates
                builder.Logging.ClearProviders();

                //Serilog configuration - Reads from appsettings.json
                builder.Services.AddSerilog((services, loggerConfig) =>
                    loggerConfig
                        .ReadFrom.Configuration(builder.Configuration)
                        .ReadFrom.Services(services)
                        .Enrich.FromLogContext()
                        .Enrich.WithProperty("ApplicationName", "GameServer.Docker"));
                        // Console sink is configured in appsettings.json - don't add it here!

                //Add configuration sources
                builder.Services.Configure<Configurations.DockerConnection>(builder.Configuration.GetSection("DockerConnection"));
                builder.Services.Configure<Configurations.PortAllocation>(builder.Configuration.GetSection("PortAllocation"));
                builder.Services.Configure<Configurations.GameTypeRegistryData>(builder.Configuration.GetSection("GameTypeRegistryData"));
                builder.Services.Configure<Configurations.GameTypeExtendedMetadataRegistryData>(builder.Configuration.GetSection("GameTypeExtendedMetadataRegistryData"));
                builder.Services.Configure<Configurations.VolumeDriverConfigOptions>(builder.Configuration.GetSection("VolumeDriverConfigOptions"));
                builder.Services.Configure<Configurations.NetworkOptions>(builder.Configuration.GetSection("NetworkOptions"));
                builder.Services.Configure<Configurations.NodeAgentOptions>(builder.Configuration.GetSection("NodeAgentOptions"));
                builder.Services.Configure<Configurations.UdpAgentDiscoveryOptions>(builder.Configuration.GetSection(Configurations.UdpAgentDiscoveryOptions.SectionName));
                builder.Services.Configure<Configurations.ServiceOperationsOptions>(builder.Configuration.GetSection("ServiceOperations"));
                builder.Services.Configure<Configurations.V2DatabaseOptions>(builder.Configuration.GetSection(Configurations.V2DatabaseOptions.SectionName));

                // Docker Client - Only needed for Direct mode
                // In Agent mode, all Docker operations are delegated to manager agents
                var serviceOpsMode = builder.Configuration.GetValue<string>("ServiceOperations:Mode") ?? "Agent";

                if (serviceOpsMode.Equals("Direct", StringComparison.OrdinalIgnoreCase))
                {
                    builder.Services.AddSingleton<DockerClientFactory>();   
                    builder.Services.AddSingleton<IDockerClient>(sp =>
                    {
                        var dockerClientFactory = sp.GetRequiredService<DockerClientFactory>();
                        return dockerClientFactory.Create();
                    });
                }
                else
                {
                    // In Agent mode, we don't need IDockerClient
                    // Provide a null implementation to satisfy any remaining dependencies
                    builder.Services.AddSingleton<IDockerClient>(sp =>
                    {
                        throw new InvalidOperationException(
                            "IDockerClient is not available when ServiceOperations:Mode=Agent. " +
                            "All Docker operations should go through IServiceOperations.");
                    });
                }

                // Agent Registry (new registration-based system) - MUST BE BEFORE ServiceOperations and NodeAgentDiscovery
                // Agents connect to the Primary Service and push their state
                // This will eventually replace NodeAgentDiscoveryService
                builder.Services.AddSingleton<IAgentRegistry, AgentRegistryService>();
                builder.Services.AddSingleton<IUdpAgentRegistry, UdpAgentRegistryService>();
                builder.Services.AddHostedService<UdpAgentAnnouncementListenerService>();

                // Service Operations - Choose implementation based on configuration
                builder.Services.AddSingleton<IServiceOperations>(sp =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    var mode = config.GetValue<string>("ServiceOperations:Mode") ?? "Direct";
                    var logger = sp.GetRequiredService<ILogger<Program>>();

                    if (mode.Equals("Agent", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogInformation("🔄 Service operations mode: AGENT (via manager node agent)");
                        return sp.GetRequiredService<ServiceOperationsViaAgent>();
                    }
                    else
                    {
                        logger.LogInformation("🔄 Service operations mode: DIRECT (via Docker client)");
                        return sp.GetRequiredService<ServiceOperationsViaDirect>();
                    }
                });

                // Register both implementations
                builder.Services.AddSingleton<ServiceOperationsViaDirect>();
                builder.Services.AddSingleton<ServiceOperationsViaAgent>();

                // DockerServiceHelper - Changed to Scoped because it depends on IGameTypeRepository (Scoped)
                builder.Services.AddScoped<DockerServiceHelper>();
                builder.Services.AddSingleton<WebHostResolver>();

                // File Management - Changed to Scoped because it depends on DockerServiceHelper
                builder.Services.AddScoped<IGameServerFileManager, GameServerFileManagerService>();

                // Server Management - Changed to Scoped because it depends on DockerServiceHelper
                builder.Services.AddScoped<IGameServerManager, GameServerManagerService>();

                // Node Agent Discovery (for real-time container stats)
                // Registered as both singleton and hosted service for background discovery
                // HttpClient instances are created per node agent for optimal connection pooling
                // Timeout is configured per-client via NodeAgentOptions in the service
                // NOTE: In Agent mode, IDockerClient is not available, so discovery service
                // will skip Docker Swarm polling and rely solely on agent registration
                builder.Services.AddHttpClient(); // Default factory for creating per-node clients
                builder.Services.AddSingleton<NodeAgentDiscoveryService>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<NodeAgentDiscoveryService>>();
                    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                    var agentOptions = sp.GetRequiredService<IOptions<Configurations.NodeAgentOptions>>();
                    var agentRegistry = sp.GetRequiredService<IAgentRegistry>();
                    var udpAgentRegistry = sp.GetRequiredService<IUdpAgentRegistry>();

                    // Try to get IDockerClient, but don't fail if unavailable (Agent mode)
                    IDockerClient? dockerClient = null;
                    if (serviceOpsMode.Equals("Direct", StringComparison.OrdinalIgnoreCase))
                    {
                        dockerClient = sp.GetRequiredService<IDockerClient>();
                    }

                    return new NodeAgentDiscoveryService(
                        logger,
                        httpClientFactory,
                        sp, // Pass IServiceProvider to avoid circular dependency
                        agentOptions,
                        agentRegistry,
                        udpAgentRegistry,
                        dockerClient);
                });
                builder.Services.AddSingleton<INodeAgentDiscovery>(sp => sp.GetRequiredService<NodeAgentDiscoveryService>());
                builder.Services.AddHostedService(sp => sp.GetRequiredService<NodeAgentDiscoveryService>());

                // Add SignalR Client for Node Agent connections (log streaming, stats streaming)
                builder.Services.AddSingleton<NodeAgentClient>();
                builder.Services.AddHostedService<Services.AgentShutdownNotificationService>();

                // Add Resource Monitoring - Changed to Scoped because it depends on DockerServiceHelper
                builder.Services.AddScoped<IGameServerResourceMonitor, GameServerResourceMonitorService>();

                // PortAllocator - Conditionally provide IDockerClient
                builder.Services.AddSingleton<PortAllocator>();/* sp =>
                {
                    var portOptions = sp.GetRequiredService<IOptions<PortAllocation>>();
                    IDockerClient? dockerClient = null;
                    if (serviceOpsMode.Equals("Direct", StringComparison.OrdinalIgnoreCase))
                    {
                        dockerClient = sp.GetRequiredService<IDockerClient>();
                    }
                    return new PortAllocator(dockerClient, portOptions);
                });*/

                // Add legacy SQLite database for the current GameType management implementation
                var connectionString = builder.Configuration.GetConnectionString("GameServerDb") 
                    ?? "Data Source=./data/gameserver.db";//let this default to a sub path to avoid NSwag build errors.
                
                // Optimize SQLite connection string for performance
                var optimizedConnectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString)
                {
                    Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
                    Cache = Microsoft.Data.Sqlite.SqliteCacheMode.Shared,
                    Pooling = true
                }.ToString();
                
                builder.Services.AddDbContext<Data.GameServerDbContext>(options =>
                {
                    options.UseSqlite(optimizedConnectionString, sqliteOptions =>
                    {
                        sqliteOptions.CommandTimeout(30); // 30 second timeout

                        // Use SplitQuery to prevent cartesian explosion with multiple collections
                        // This fixes the EF Core warning about QuerySplittingBehavior
                        sqliteOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                    });

                    // Disable sensitive data logging in production for performance
                    if (builder.Environment.IsDevelopment())
                    {
                        options.EnableSensitiveDataLogging();
                    }

                    // Lazy initialization - don't validate connections during service registration
                    options.EnableServiceProviderCaching(false);
                });

                // Add separate V2 database context for the new persistence implementation.
                // Provider selection is isolated from the legacy DbContext so both paths can coexist.
                builder.Services.AddDbContext<DataV2.GameServerV2DbContext>((serviceProvider, options) =>
                {
                    var v2Options = serviceProvider.GetRequiredService<IOptions<Configurations.V2DatabaseOptions>>().Value;
                    var provider = v2Options.Provider;
                    var connectionName = string.IsNullOrWhiteSpace(v2Options.ConnectionStringName)
                        ? "GameServerV2Db"
                        : v2Options.ConnectionStringName;

                    var v2ConnectionString = builder.Configuration.GetConnectionString(connectionName)
                        ?? builder.Configuration.GetConnectionString("GameServerDb")
                        ?? "Data Source=./data/gameserver-v2.db";

                    DataV2.GameServerV2DbContextFactory.ConfigureProvider(
                        (DbContextOptionsBuilder)options,
                        provider,
                        v2ConnectionString);

                    if (builder.Environment.IsDevelopment())
                    {
                        options.EnableSensitiveDataLogging();
                    }

                    options.EnableServiceProviderCaching(false);
                });

                // Add GameType Repository (database-backed) - This replaces file-based registries
                // Register IMemoryCache for GameType caching
                builder.Services.AddMemoryCache();
                builder.Services.AddScoped<Repositories.IGameTypeRepository, Repositories.GameTypeRepository>();
                builder.Services.AddScoped<RepositoriesV2.IGameTypeRepository, RepositoriesV2.GameTypeRepository>();
                builder.Services.AddScoped<RepositoriesV2.IGameServerRepository, RepositoriesV2.GameServerRepository>();
                builder.Services.AddScoped<ServicesV2.GameTypeQueryService>();
                builder.Services.AddScoped<ServicesV2.GameTypeCommandService>();
                builder.Services.AddScoped<ServicesV2Detection.GameTypeSetupDetectionService>(sp =>
                {
                    IDockerClient? dockerClient = null;
                    if (serviceOpsMode.Equals("Direct", StringComparison.OrdinalIgnoreCase))
                    {
                        dockerClient = sp.GetRequiredService<IDockerClient>();
                    }

                    return new ServicesV2Detection.GameTypeSetupDetectionService(
                        sp.GetRequiredService<RepositoriesV2.IGameTypeRepository>(),
                        dockerClient,
                        sp.GetRequiredService<ILogger<ServicesV2Detection.GameTypeSetupDetectionService>>());
                });

                // Keep file-based registries as fallback/migration helpers (optional)
                // builder.Services.AddSingleton<IGameTypeRegistry, GaneTypeRegistryFile>();
                // builder.Services.AddSingleton<IGameTypeExtendedMetadataRegistry, GameTypeExtendedMetadataRegistryFile>();

                // GameTypeMetadataApplier - Changed to Scoped because it depends on IGameTypeRepository (Scoped)
                builder.Services.AddScoped<GameTypeMetadataApplier>();

                // Database Initialization - Runs in background after webhost starts
                // This allows the webhost and SignalR hubs to be available immediately
                // Skip if running under NSwag or if --no-db-init flag is present
                var entryAssembly = System.Reflection.Assembly.GetEntryAssembly()?.Location ?? "";
                var commandLine = Environment.CommandLine;
                var isNSwagExecution = entryAssembly.Contains("NSwag", StringComparison.OrdinalIgnoreCase) ||
                                       commandLine.Contains("NSwag", StringComparison.OrdinalIgnoreCase);
                var skipDbInit = args.Contains("--no-db-init") || 
                                 Environment.GetEnvironmentVariable("SKIP_DB_INIT") == "true" ||
                                 isNSwagExecution;

                if (!skipDbInit)
                {
                    builder.Services.AddHostedService<Services.DatabaseInitializationService>();
                }
                else
                {
                    Log.Debug("Database initialization will be skipped (NSwag={NSwag}, Entry={Entry})", isNSwagExecution, entryAssembly);
                }

                //// ServerLifecycleService - Conditionally provide IDockerClient
                //// NOTE: This service is deprecated and should be refactored to use IServiceOperations
                //builder.Services.AddScoped<ServerLifecycleService>(sp =>
                //{
                //    IDockerClient? dockerClient = null;
                //    if (serviceOpsMode.Equals("Direct", StringComparison.OrdinalIgnoreCase))
                //    {
                //        dockerClient = sp.GetRequiredService<IDockerClient>();
                //    }
                //    return new ServerLifecycleService(dockerClient);
                //});

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

                // Terminal Session Manager (singleton for long-lived terminal sessions)
                builder.Services.AddSingleton<Services.TerminalSessionManager>();

                WebApplication app;
                try
                {
                    app = builder.Build();
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, "Failed to build WebApplication. Dependency Injection error:");

                    // Log all inner exceptions for AggregateException
                    if (ex is AggregateException aggEx)
                    {
                        Log.Fatal("AggregateException with {Count} inner exceptions:", aggEx.InnerExceptions.Count);
                        foreach (var innerEx in aggEx.InnerExceptions)
                        {
                            Log.Fatal(innerEx, "  → Inner Exception: {Message}", innerEx.Message);
                        }
                    }

                    Log.CloseAndFlush();
                    Environment.ExitCode = 1;
                    throw;
                }

                var mainLogger = app.Services.GetRequiredService<ILogger<Program>>();
                mainLogger.LogInformation($"🚀 WebHost built successfully. Configuring middleware...");

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

                // Only use HTTPS redirection in development with proper HTTPS setup
                // In Docker/Production, this is typically handled by a reverse proxy
                if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("UseHttpsRedirection", false))
                {
                    app.UseHttpsRedirection();
                }

                // Enable CORS (must be before routing)
                app.UseCors();

                app.UseAuthorization();

                // Map SignalR hubs
                app.MapHub<Hubs.ContainerConsoleHub>("/hubs/console");   // TTY attach (read-only)
                app.MapHub<Hubs.ContainerConsoleHub>("/hubs/terminal");  // Interactive exec shell
                app.MapHub<Hubs.ServerLogsHub>("/hubs/serverlogs");
                app.MapHub<Hubs.ResourceMonitoringHub>("/hubs/resources");
                app.MapHub<Hubs.AgentRegistrationHub>("/hubs/agentregistration"); // Agent registration

                app.MapControllers();

                mainLogger.LogInformation("🎯 WebHost has started listening. Database initialization is still running in the background; the application will shut down if initialization fails.");

                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");

                // Log all inner exceptions for AggregateException
                if (ex is AggregateException aggEx)
                {
                    foreach (var innerEx in aggEx.InnerExceptions)
                    {
                        Log.Fatal(innerEx, "  Inner Exception: {Message}", innerEx.Message);
                    }
                }

                // Ensure we exit with error code
                Environment.ExitCode = 1;
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
