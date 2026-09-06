using GameServer.API.Interfaces;
using GameServer.API.Services;
using GameServer.Catalog;
using GameServer.Deployment;
using GameServer.Monitoring;
using GameServer.Orchestration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using System.Reflection;
using Scalar.AspNetCore;

namespace GameServer.API
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
                Log.Information("Starting GameServer.API Version {AssemblyVersion} (Informational: {InformationalVersion})", assemblyVersion, informationalVersion);

                var builder = WebApplication.CreateBuilder(args);

                // Clear default logging providers to prevent duplicates
                builder.Logging.ClearProviders();

                //Serilog configuration - Reads from appsettings.json
                builder.Services.AddSerilog((services, loggerConfig) =>
                    loggerConfig
                        .ReadFrom.Configuration(builder.Configuration)
                        .ReadFrom.Services(services)
                        .Enrich.FromLogContext()
                        .Enrich.WithProperty("ApplicationName", "GameServer.API")
                        .Enrich.WithProperty("ApplicationVersion", assemblyVersion)
                        .Enrich.WithProperty("ApplicationInformationalVersion", informationalVersion));
                        // Console sink is configured in appsettings.json - don't add it here!

                builder.Services.Configure<Configurations.V2DatabaseOptions>(builder.Configuration.GetSection(Configurations.V2DatabaseOptions.SectionName));

                // Bind modular-hosting options up front so we can branch the
                // Orchestration + hub registrations below.
                var modularHosting = builder.Configuration
                    .GetSection(Configurations.ModularHostingOptions.SectionName)
                    .Get<Configurations.ModularHostingOptions>() ?? new Configurations.ModularHostingOptions();
                builder.Services.Configure<Configurations.ModularHostingOptions>(
                    builder.Configuration.GetSection(Configurations.ModularHostingOptions.SectionName));

                if (modularHosting.UseHttpAgentRegistry)
                {
                    // Delegate agent registry + discovery to the standalone GameServer.Orchestration.Host.
                    builder.Services.AddOrchestrationHttpClientModule(builder.Configuration);
                }
                else
                {
                    // Add Orchestration module (Agent registry, discovery, SignalR client,
                    // session manager, and ITerminalSessionNotifier bound to ContainerConsoleHub).
                    builder.Services.AddOrchestrationModule(builder.Configuration);
                    builder.Services.AddSingleton<IAgentShutdownNotifier, SignalRAgentShutdownNotifier>();
                }

                // Add Deployment module (Spec builder, volume resolver, port allocator, command/deployment services)
                builder.Services.AddDeploymentModule(builder.Configuration);

                // Database Initialization flag - Skip if running under NSwag or if --no-db-init flag is present
                var entryAssembly = System.Reflection.Assembly.GetEntryAssembly()?.Location ?? "";
                var commandLine = Environment.CommandLine;
                var isNSwagExecution = entryAssembly.Contains("NSwag", StringComparison.OrdinalIgnoreCase) ||
                                       commandLine.Contains("NSwag", StringComparison.OrdinalIgnoreCase);
                var skipDbInit = args.Contains("--no-db-init") || 
                                 Environment.GetEnvironmentVariable("SKIP_DB_INIT") == "true" ||
                                 isNSwagExecution;

                // Add Catalog module (DbContext, Migrations, Repositories, Setup Detection, DB Init)
                builder.Services.AddCatalogModule(builder.Configuration, builder.Environment, skipDbInit);

                // Add Monitoring module (Resource monitors, collectors, aggregators, query services)
                builder.Services.AddMonitoringModule();

                builder.Services.AddControllers();
                
                // Add SignalR for real-time features (console, logs, monitoring)
                builder.Services.AddSignalR();

                // When modular hosting is enabled, register YARP so the API can act as a
                // gateway that relays SignalR hubs and standalone-host REST endpoints to
                // GameServer.Orchestration.Host and GameServer.Monitoring.Host. The Web UI
                // continues to talk to GameServer.API only.
                if (!modularHosting.HostHubsInApi)
                {
                    var orchestrationBase = builder.Configuration
                        .GetSection(GameServer.Orchestration.OrchestrationServiceOptions.SectionName)
                        .Get<GameServer.Orchestration.OrchestrationServiceOptions>()
                        ?? new GameServer.Orchestration.OrchestrationServiceOptions();
                    var monitoringBase = builder.Configuration
                        .GetSection(Configurations.MonitoringServiceOptions.SectionName)
                        .Get<Configurations.MonitoringServiceOptions>()
                        ?? new Configurations.MonitoringServiceOptions();

                    var routes = new[]
                    {
                        BuildHubRoute("hub-agentregistration", "/hubs/agentregistration/{**catch-all}", "orchestration-cluster"),
                        BuildHubRoute("hub-terminal",          "/hubs/terminal/{**catch-all}",          "orchestration-cluster"),
                        BuildHubRoute("hub-resources",         "/hubs/resources/{**catch-all}",         "monitoring-cluster"),
                        BuildHubRoute("hub-serverlogs",        "/hubs/serverlogs/{**catch-all}",        "monitoring-cluster"),
                        BuildHubRoute("hub-attach",            "/hubs/attach/{**catch-all}",            "monitoring-cluster"),
                    };

                    var clusters = new[]
                    {
                        new Yarp.ReverseProxy.Configuration.ClusterConfig
                        {
                            ClusterId = "orchestration-cluster",
                            Destinations = new Dictionary<string, Yarp.ReverseProxy.Configuration.DestinationConfig>
                            {
                                ["primary"] = new() { Address = orchestrationBase.BaseUrl }
                            }
                        },
                        new Yarp.ReverseProxy.Configuration.ClusterConfig
                        {
                            ClusterId = "monitoring-cluster",
                            Destinations = new Dictionary<string, Yarp.ReverseProxy.Configuration.DestinationConfig>
                            {
                                ["primary"] = new() { Address = monitoringBase.BaseUrl }
                            }
                        },
                    };

                    builder.Services.AddReverseProxy().LoadFromMemory(routes, clusters);
                }

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
                        document.Info.Title = "GameServer.API";
                        document.Info.Version = "v1";
                        document.Info.Description = "GameServer.API ASP.NET Core Web API";
                        return Task.CompletedTask;
                    });
                });

                // Add NSwag document generator services (required for client code generation)
                // Note: We only add the generator services, not the middleware
                builder.Services.AddOpenApiDocument(opts =>
                {
                    opts.Title = "GameServer.API";
                    opts.Version = "v1";
                    opts.Description = "GameServer.API ASP.NET Core Web API";
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
                mainLogger.LogInformation("GameServer.API runtime version {AssemblyVersion} (Informational: {InformationalVersion})", assemblyVersion, informationalVersion);
                mainLogger.LogInformation($"🚀 WebHost built successfully. Configuring middleware...");

                // Add Serilog request logging with clean handling for client-aborted requests
                app.UseSerilogRequestLogging(options =>
                {
                    options.GetLevel = (httpContext, elapsed, ex) =>
                    {
                        if (ex is OperationCanceledException || httpContext.Response.StatusCode == 499)
                        {
                            return LogEventLevel.Debug;
                        }

                        if (ex != null || httpContext.Response.StatusCode >= 500)
                        {
                            return LogEventLevel.Error;
                        }

                        return LogEventLevel.Information;
                    };
                });

                // Handle client-aborted requests cleanly without unhandled 500 errors
                app.Use(async (context, next) =>
                {
                    try
                    {
                        await next();
                    }
                    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
                    {
                        context.Response.StatusCode = 499; // Client Closed Request
                    }
                });

                // Map OpenAPI endpoint and Scalar UI
                app.MapOpenApi();
                app.MapScalarApiReference(options =>
                {
                    options.WithTitle("GameServer.API");
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

                // Map SignalR hubs (only when this host owns them — in modular deployments
                // ownership moves to GameServer.Orchestration.Host and GameServer.Monitoring.Host).
                if (modularHosting.HostHubsInApi)
                {
                    app.MapHub<Hubs.ContainerAttachHub>("/hubs/attach");      // Shared multi-subscriber container attach
                    app.MapHub<GameServer.Orchestration.Hubs.ContainerConsoleHub>("/hubs/terminal");  // Interactive exec shell (per-user)
                    app.MapHub<Hubs.ServerLogsHub>("/hubs/serverlogs");
                    app.MapHub<Hubs.ResourceMonitoringHub>("/hubs/resources");
                    app.MapHub<Hubs.AgentRegistrationHub>("/hubs/agentregistration"); // Agent registration
                }
                else
                {
                    mainLogger.LogInformation("ModularHosting:HostHubsInApi is false — SignalR hubs are relayed to GameServer.Orchestration.Host and GameServer.Monitoring.Host via YARP.");
                    app.MapReverseProxy();
                }

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

        private static Yarp.ReverseProxy.Configuration.RouteConfig BuildHubRoute(string routeId, string path, string clusterId)
            => new()
            {
                RouteId = routeId,
                ClusterId = clusterId,
                Match = new Yarp.ReverseProxy.Configuration.RouteMatch { Path = path },
            };
    }
}
