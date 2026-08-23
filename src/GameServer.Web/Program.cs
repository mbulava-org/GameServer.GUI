using GameServer.API.Client;
using GameServer.API.Client.Extensions;
using GameServer.Web.Components;
using GameServer.Web.Services;
using Microsoft.Extensions.FileProviders;
using Radzen;
using Serilog;
using System.Reflection;

namespace GameServer.Web
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
                var assembly = Assembly.GetExecutingAssembly();
                var assemblyVersion = assembly.GetName().Version?.ToString() ?? "unknown";
                var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? assemblyVersion;
                Log.Information("Starting GameServer.Web Version {AssemblyVersion} (Informational: {InformationalVersion})", assemblyVersion, informationalVersion);


                var builder = WebApplication.CreateBuilder(args);

                // Clear default logging providers to prevent duplicates
                builder.Logging.ClearProviders();

                //Serilog configuration - Console sink from code only (no appsettings.json config)
                builder.Services.AddSerilog((services, loggerConfig) =>
                    loggerConfig
                        .ReadFrom.Configuration(builder.Configuration)
                        .ReadFrom.Services(services)
                        .Enrich.FromLogContext()
                        .Enrich.WithProperty("ApplicationName", "GameServer.Web")
                        .Enrich.WithProperty("ApplicationVersion", assemblyVersion)
                        .Enrich.WithProperty("ApplicationInformationalVersion", informationalVersion)
                        .WriteTo.Console(
                            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"));


                // Bind API client configuration directly from appsettings.json.
                builder.Services.AddSingleton(builder.Configuration.GetSection("GameServerDockerApi").Get<Configurations.GameServerDockerApi>() ?? new Configurations.GameServerDockerApi());

                // Add services to the container.
                builder.Services.AddRazorComponents()
                    .AddInteractiveServerComponents(options =>
                    {
                        // Only surface detailed server-side exception text to the browser in Development.
                        // In production (Docker/reverse proxy) keep this off so internal details aren't leaked.
                        options.DetailedErrors = builder.Environment.IsDevelopment();
                    });
                builder.Services.AddRadzenComponents();

                // When running behind a published Docker port and/or a reverse proxy, the Blazor Server
                // circuit negotiates the '/_blazor' WebSocket against the scheme/host the browser sees.
                // Honor X-Forwarded-* headers so the circuit connects using the correct external scheme/host.
                builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
                {
                    options.ForwardedHeaders =
                        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto |
                        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost;

                    // The proxies/networks are not known ahead of time in container deployments,
                    // so clear the defaults to accept forwarded headers from the docker/proxy network.
                    options.KnownNetworks.Clear();
                    options.KnownProxies.Clear();
                });



                // Register WebSocket service as singleton
                //builder.Services.AddSingleton<GameServerWebSocketService>();
                builder.Services.AddHttpClient();
                builder.Services.Configure<ThumbnailCacheOptions>(options =>
                {
                    options.CacheDirectory = Path.Combine(Path.GetTempPath(), "GameServer.Web", "thumbnail-cache");
                    options.RequestPath = "/thumbnail-cache";
                    Directory.CreateDirectory(options.CacheDirectory);
                });
                builder.Services.AddScoped<IThumbnailCacheService, ThumbnailCacheService>();
                builder.Services.AddScoped<Services.V2.IGameServerV2ApiService, Services.V2.GameServerV2ApiService>();
                builder.Services.AddScoped<Services.V2.IGameTypeV2ApiService, Services.V2.GameTypeV2ApiService>();
                builder.Services.AddScoped<Services.V2.IMountTypeConfigApiService, Services.V2.MountTypeConfigApiService>();
                builder.Services.AddScoped<Services.V2.IGameServerFilesApiService, Services.V2.GameServerFilesApiService>();

                //Simplification???
                var apiBaseUrl = builder.Configuration["GameServerDockerApi:BaseUri"] ?? "http://localhost:5164/";
                var wsBaseUrl = apiBaseUrl.Replace("https://", "wss://").Replace("http://", "ws://");
                var consoleUri = wsBaseUrl + "hubs/attach";
                var resourcesUri = wsBaseUrl + "hubs/resources";
                var terminalUri = wsBaseUrl + "hubs/terminal";
                builder.Services.AddGameServerClients(apiBaseUrl, consoleUri, resourcesUri, terminalUri);
                //{
                //    builder.Services.AddContainerConsoleClient(consoleUri);
                //}

                //if (!string.IsNullOrWhiteSpace(resourcesUri))
                //{
                //    builder.Services.AddResourceMonitoringClient(resourcesUri);
                //}



                var app = builder.Build();
                app.Logger.LogInformation("GameServer.Web runtime version {AssemblyVersion} (Informational: {InformationalVersion})", assemblyVersion, informationalVersion);

                // Apply forwarded headers first so downstream middleware and the Blazor circuit see the
                // external scheme/host (published Docker port today, reverse proxy later).
                app.UseForwardedHeaders();

                // Configure the HTTP request pipeline.
                if (!app.Environment.IsDevelopment())
                {
                    app.UseExceptionHandler("/Error");
                    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                    app.UseHsts();
                }

                app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

                // HTTPS redirection is only safe when an HTTPS port is actually configured.
                // In Docker (and behind the reverse proxy) TLS is terminated upstream and no HTTPS port
                // is bound, so redirecting here fails ("Failed to determine the https port for redirect")
                // and can disrupt the Blazor '/_blazor' circuit negotiation. Only enable it when a port exists.
                var httpsPort = builder.Configuration["HTTPS_PORT"]
                    ?? builder.Configuration["ASPNETCORE_HTTPS_PORT"]
                    ?? builder.Configuration["ASPNETCORE_HTTPS_PORTS"];
                if (!string.IsNullOrWhiteSpace(httpsPort))
                {
                    app.UseHttpsRedirection();
                }

                // Critical: Serve static files from wwwroot AND from Razor Class Libraries (_content)
                // In .NET 10, this automatically uses the staticwebassets.runtime.json manifest
                app.UseStaticFiles();

                var thumbnailCacheOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ThumbnailCacheOptions>>().Value;
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(thumbnailCacheOptions.CacheDirectory),
                    RequestPath = thumbnailCacheOptions.RequestPath
                });

                // Enable WebSockets so the Blazor Server circuit ('/_blazor') can use the WebSocket
                // transport instead of falling back/failing when hosted in Docker or behind a proxy.
                app.UseWebSockets();

                app.UseAntiforgery();

                // Map static assets (for .NET 10+ optimizations)
                app.MapStaticAssets();
                
                app.MapRazorComponents<App>()
                    .AddInteractiveServerRenderMode();

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
