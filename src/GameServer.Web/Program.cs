using GameServer.Docker.Client;
using GameServer.Docker.Client.Extensions;
using GameServer.Web.Components;
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
                //Log startup information
                var asmb = Assembly.GetExecutingAssembly();
                Log.Information($"Starting GameServer.Web Version - {asmb.GetName().Version}");


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
                        .WriteTo.Console(
                            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"));


                // Bind API client configuration directly from appsettings.json.
                builder.Services.AddSingleton(builder.Configuration.GetSection("GameServerDockerApi").Get<Configurations.GameServerDockerApi>() ?? new Configurations.GameServerDockerApi());

                // Add services to the container.
                builder.Services.AddRazorComponents()
                    .AddInteractiveServerComponents();
                builder.Services.AddRadzenComponents();



                // Register WebSocket service as singleton
                //builder.Services.AddSingleton<GameServerWebSocketService>();
                builder.Services.AddHttpClient();
                builder.Services.AddScoped<Services.V2.GameServerV2ApiService>();
                builder.Services.AddScoped<Services.V2.GameTypeV2ApiService>();
                builder.Services.AddScoped<Services.V2.MountTypeConfigApiService>();

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

                // Configure the HTTP request pipeline.
                if (!app.Environment.IsDevelopment())
                {
                    app.UseExceptionHandler("/Error");
                    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                    app.UseHsts();
                }

                app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
                
                // HTTPS redirection (disabled in production/Docker - handled by reverse proxy)
                if (app.Environment.IsDevelopment())
                {
                    app.UseHttpsRedirection();
                }

                // Critical: Serve static files from wwwroot AND from Razor Class Libraries (_content)
                // In .NET 10, this automatically uses the staticwebassets.runtime.json manifest
                app.UseStaticFiles();
                
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
