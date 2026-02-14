using GameServer.Docker.Client;
using GameServer.Docker.Client.Extensions;
using GameServer.Web.Components;
using Radzen;

namespace GameServer.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.Configure<Configurations.GameServerDockerApi>(
                builder.Configuration.GetSection("GameServerDockerApi"));

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();
            builder.Services.AddRadzenComponents();


            
            // Register WebSocket service as singleton
            //builder.Services.AddSingleton<GameServerWebSocketService>();
            builder.Services.AddHttpClient();

            //Simplification???
            var apiBaseUrl = builder.Configuration["GameServerDockerApi:BaseUri"] ?? "http://localhost:5164/";
            var consoleUri = apiBaseUrl.Replace("https://", "wss://").Replace("http://", "ws://") + "hubs/console";
            var resourcesUri = apiBaseUrl.Replace("https://", "wss://").Replace("http://", "ws://") + "hubs/resources";
            //builder.Services.AddGameServerClients(apiBaseUrl, consoleUri, resourcesUri);
            builder.Services.AddHttpClient<IDashboardApi, DashboardApi>(client =>
            {
                client.BaseAddress = new Uri(apiBaseUrl);
            });
            builder.Services.AddHttpClient<IGameTypeApi, GameTypeApi>(client =>
            {
                client.BaseAddress = new Uri(apiBaseUrl);
            });
            builder.Services.AddHttpClient<IGameTypeExtendedMetadataApi, GameTypeExtendedMetadataApi>(client =>
            {
                client.BaseAddress = new Uri(apiBaseUrl);
            });
            builder.Services.AddHttpClient<IPortApi, PortApi>(client =>
            {
                client.BaseAddress = new Uri(apiBaseUrl);
            });
            builder.Services.AddHttpClient<IGameServerApi, GameServerApi>(client =>
            {
                client.BaseAddress = new Uri(apiBaseUrl);
            });

            // TODO: Uncomment once GameServer.Docker.Client includes PortRelationship, PortValidationRule models
            // This requires the API to expose these models through controller endpoints
            // Register Port Mapping Service for advanced port validation and automatic updates
            // builder.Services.AddScoped<GameServer.Web.Services.PortMappingService>();

            // Register SignalR clients if URLs provided
            if (!string.IsNullOrWhiteSpace(consoleUri))
            {
                builder.Services.AddContainerConsoleClient(consoleUri);
            }

            if (!string.IsNullOrWhiteSpace(resourcesUri))
            {
                builder.Services.AddResourceMonitoringClient(resourcesUri);
            }



            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            app.UseHttpsRedirection();

            app.UseStaticFiles(); // Add explicit static files middleware
            app.UseAntiforgery();

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }
    }
}
