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

                // Add Orchestration module (Agent registry, discovery, SignalR client, session manager)
                builder.Services.AddOrchestrationModule(builder.Configuration);
                builder.Services.AddSingleton<ITerminalSessionNotifier, SignalRTerminalSessionNotifier>();
                builder.Services.AddSingleton<IAgentShutdownNotifier, SignalRAgentShutdownNotifier>();

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
