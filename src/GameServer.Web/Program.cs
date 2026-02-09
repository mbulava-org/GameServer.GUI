using GameServer.Docker.Client;
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

            var apiBaseUrl = builder.Configuration["GameServerDockerApi:BaseUri"] ?? "http://localhost:5164/";

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
