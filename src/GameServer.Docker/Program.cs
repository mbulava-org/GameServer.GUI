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
                var assembly = Assembly.GetExecutingAssembly();
                var assemblyVersion = assembly.GetName().Version?.ToString() ?? "unknown";
                var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? assemblyVersion;
                Log.Information("Starting GameServer.Docker Version {AssemblyVersion} (Informational: {InformationalVersion})", assemblyVersion, informationalVersion);

                var builder = WebApplication.CreateBuilder(args);

                // Clear default logging providers to prevent duplicates
                builder.Logging.ClearProviders();

                //Serilog configuration - Reads from appsettings.json
                builder.Services.AddSerilog((services, loggerConfig) =>
                    loggerConfig
                        .ReadFrom.Configuration(builder.Configuration)
                        .ReadFrom.Services(services)
                        .Enrich.FromLogContext()
                        .Enrich.WithProperty("ApplicationName", "GameServer.Docker")
                        .Enrich.WithProperty("ApplicationVersion", assemblyVersion)
                        .Enrich.WithProperty("ApplicationInformationalVersion", informationalVersion));
                        // Console sink is configured in appsettings.json - don't add it here!

                // Bind configuration classes directly from appsettings.json.
                builder.Services.AddSingleton(builder.Configuration.GetSection("PortAllocation").Get<Configurations.PortAllocation>() ?? new Configurations.PortAllocation());
                builder.Services.AddSingleton(builder.Configuration.GetSection("NetworkOptions").Get<Configurations.NetworkOptions>() ?? new Configurations.NetworkOptions());
                builder.Services.AddSingleton(builder.Configuration.GetSection("NodeAgentOptions").Get<Configurations.NodeAgentOptions>() ?? new Configurations.NodeAgentOptions());
                builder.Services.AddSingleton(builder.Configuration.GetSection(Configurations.UdpAgentDiscoveryOptions.SectionName).Get<Configurations.UdpAgentDiscoveryOptions>() ?? new Configurations.UdpAgentDiscoveryOptions());
                builder.Services.Configure<Configurations.V2DatabaseOptions>(builder.Configuration.GetSection(Configurations.V2DatabaseOptions.SectionName));

                // Gate that flips to "ready" once background database initialization (migrations + seeding)
                // has completed. Requests made while a migration is mid-flight (e.g. a column rename) can
                // otherwise fail with transient "Unknown column" errors, so API requests are held back with
                // a 503 until this is signaled.
                builder.Services.AddSingleton<Services.IDatabaseReadinessGate, Services.DatabaseReadinessGate>();

                // Agent Registry (new registration-based system) - MUST BE BEFORE ServiceOperations and NodeAgentDiscovery
                // Agents connect to the Primary Service and push their state
                // This will eventually replace NodeAgentDiscoveryService
                builder.Services.AddSingleton<IAgentRegistry, AgentRegistryService>();
                builder.Services.AddSingleton<IUdpAgentRegistry, UdpAgentRegistryService>();
                builder.Services.AddHostedService<UdpAgentAnnouncementListenerService>();

                // Service Operations - Always use agent-based implementation
                builder.Services.AddSingleton<IServiceOperations, ServiceOperationsViaAgent>();

                // Node Agent Discovery (for real-time container stats)
                // Registered as both singleton and hosted service for background discovery
                // HttpClient instances are created per node agent for optimal connection pooling
                // Timeout is configured per-client via NodeAgentOptions in the service
                builder.Services.AddHttpClient(); // Default factory for creating per-node clients
                builder.Services.AddSingleton<NodeAgentDiscoveryService>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<NodeAgentDiscoveryService>>();
                    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                    var agentOptions = sp.GetRequiredService<Configurations.NodeAgentOptions>();
                    var agentRegistry = sp.GetRequiredService<IAgentRegistry>();
                    var udpAgentRegistry = sp.GetRequiredService<IUdpAgentRegistry>();

                    return new NodeAgentDiscoveryService(
                        logger,
                        httpClientFactory,
                        sp, // Pass IServiceProvider to avoid circular dependency
                        agentOptions,
                        agentRegistry,
                        udpAgentRegistry);
                });
                builder.Services.AddSingleton<INodeAgentDiscovery>(sp => sp.GetRequiredService<NodeAgentDiscoveryService>());
                builder.Services.AddHostedService(sp => sp.GetRequiredService<NodeAgentDiscoveryService>());

                // Add SignalR Client for Node Agent connections (log streaming, stats streaming)
                builder.Services.AddSingleton<NodeAgentClient>();
                builder.Services.AddHostedService<Services.AgentShutdownNotificationService>();

                // PortAllocator
                builder.Services.AddSingleton<PortAllocator>();

                // Add V2 database context for the persistence implementation.
                // Provider selection is isolated from the legacy DbContext so both paths can coexist.
                // The provider-specific subclasses (Sqlite/MySql) own their EF Core migration sets, so the
                // matching concrete context must be registered for MigrateAsync() to discover its migrations.
                // GameServerV2DbContext is aliased to whichever concrete context is active so repositories
                // (which depend on the base type) resolve the correct instance.
                {
                    var v2Options = builder.Configuration
                        .GetSection(Configurations.V2DatabaseOptions.SectionName)
                        .Get<Configurations.V2DatabaseOptions>() ?? new Configurations.V2DatabaseOptions();
                    var provider = (v2Options.Provider ?? "sqlite").Trim().ToLowerInvariant();
                    var defaultConnectionName = provider switch
                    {
                        "postgres" or "postgresql" => "GameServerV2PostgresDb",
                        "mysql" => "GameServerV2MySqlDb",
                        _ => "GameServerV2Db"
                    };
                    var connectionName = string.IsNullOrWhiteSpace(v2Options.ConnectionStringName)
                        ? defaultConnectionName
                        : v2Options.ConnectionStringName;

                    var v2ConnectionString = builder.Configuration.GetConnectionString(connectionName)
                        ?? builder.Configuration.GetConnectionString(defaultConnectionName)
                        ?? builder.Configuration.GetConnectionString("GameServerV2Db")
                        ?? "Data Source=./data/gameserver-v2.db";

                    void ConfigureV2Options(DbContextOptionsBuilder options)
                    {
                        DataV2.GameServerV2DbContextFactory.ConfigureProvider(
                            options,
                            provider,
                            v2ConnectionString);

                        if (builder.Environment.IsDevelopment())
                        {
                            options.EnableSensitiveDataLogging();
                        }

                        options.EnableServiceProviderCaching(false);
                    }

                    switch (provider)
                    {
                        case "mysql":
                            builder.Services.AddDbContext<DataV2.MySqlGameServerV2DbContext>((_, options) => ConfigureV2Options(options));
                            builder.Services.AddScoped<DataV2.GameServerV2DbContext>(sp => sp.GetRequiredService<DataV2.MySqlGameServerV2DbContext>());
                            break;

                        case "postgres":
                        case "postgresql":
                            // PostgreSQL schema is deployed via the dedicated pgPac database project, so the
                            // base context (without an EF migration set) is used directly.
                            builder.Services.AddDbContext<DataV2.GameServerV2DbContext>((_, options) => ConfigureV2Options(options));
                            break;

                        default:
                            builder.Services.AddDbContext<DataV2.SqliteGameServerV2DbContext>((_, options) => ConfigureV2Options(options));
                            builder.Services.AddScoped<DataV2.GameServerV2DbContext>(sp => sp.GetRequiredService<DataV2.SqliteGameServerV2DbContext>());
                            break;
                    }
                }

                // Add V2 repositories
                // Register IMemoryCache for GameType caching
                builder.Services.AddMemoryCache();
                builder.Services.AddScoped<RepositoriesV2.IGameTypeRepository, RepositoriesV2.GameTypeRepository>();
                builder.Services.AddScoped<RepositoriesV2.IGameServerRepository, RepositoriesV2.GameServerRepository>();
                builder.Services.AddScoped<RepositoriesV2.IMountTypeConfigRepository, RepositoriesV2.MountTypeConfigRepository>();
                builder.Services.AddScoped<ServicesV2.GameServerQueryService>();
                builder.Services.AddScoped<ServicesV2.GameServerValidationService>();
                builder.Services.AddScoped<ServicesV2.GameServerCommandService>();
                builder.Services.AddScoped<ServicesV2.GameTypeQueryService>();
                builder.Services.AddScoped<ServicesV2.GameTypeCommandService>();
                builder.Services.AddScoped<ServicesV2.IVolumeSetupResolver, ServicesV2.VolumeSetupResolver>();
                builder.Services.Configure<GameServer.Docker.Configurations.NfsPreparationOptions>(
                    builder.Configuration.GetSection(GameServer.Docker.Configurations.NfsPreparationOptions.SectionName));
                builder.Services.AddScoped<ServicesV2.INfsVolumePreparationService, ServicesV2.NfsVolumePreparationService>();
                builder.Services.AddScoped<ServicesV2Detection.GameTypeSetupDetectionService>(sp =>
                    new ServicesV2Detection.GameTypeSetupDetectionService(
                        sp.GetRequiredService<RepositoriesV2.IGameTypeRepository>(),
                        sp.GetRequiredService<IAgentRegistry>(),
                        sp.GetRequiredService<IHttpClientFactory>(),
                        sp.GetRequiredService<ILogger<ServicesV2Detection.GameTypeSetupDetectionService>>()));

                // V2-compatible resource monitor/aggregator for SignalR streaming hubs
                builder.Services.AddScoped<Interfaces.IServerResourceMonitor, ServicesV2.ServerResourceMonitor>();
                builder.Services.AddSingleton<Interfaces.IServerResourceAggregator, ServicesV2.ServerResourceAggregator>();
                builder.Services.AddSingleton<Interfaces.IServerLogAggregator, ServicesV2.ServerLogAggregator>();
                builder.Services.AddSingleton<Interfaces.IContainerAttachAggregator, ServicesV2.ContainerAttachAggregator>();

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
                mainLogger.LogInformation("GameServer.Docker runtime version {AssemblyVersion} (Informational: {InformationalVersion})", assemblyVersion, informationalVersion);
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

                // Reject API requests with 503 while database initialization is still running in the
                // background, so clients don't get transient "Unknown column" errors mid-migration.
                app.Use(async (context, next) =>
                {
                    var readinessGate = context.RequestServices.GetRequiredService<Services.IDatabaseReadinessGate>();
                    if (!readinessGate.IsReady && context.Request.Path.StartsWithSegments("/api"))
                    {
                        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                        context.Response.Headers.RetryAfter = "2";
                        await context.Response.WriteAsJsonAsync(new
                        {
                            error = "Database initialization is still in progress. Please retry shortly."
                        });
                        return;
                    }

                    await next();
                });

                // Map SignalR hubs
                app.MapHub<Hubs.ContainerAttachHub>("/hubs/attach");      // Shared multi-subscriber container attach
                app.MapHub<Hubs.ContainerConsoleHub>("/hubs/terminal");  // Interactive exec shell (per-user)
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
