using GameServer.API.Interfaces;
using GameServer.API.Services.V2;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Monitoring
{
    public static class MonitoringServiceExtensions
    {
        public static IServiceCollection AddMonitoringModule(this IServiceCollection services)
        {
            services.AddScoped<GameServerQueryService>();
            services.AddScoped<IGameServerQueryService>(sp => sp.GetRequiredService<GameServerQueryService>());
            services.AddScoped<GameTypeQueryService>();
            services.AddScoped<IGameTypeQueryService>(sp => sp.GetRequiredService<GameTypeQueryService>());

            services.AddScoped<IServerResourceMonitor, ServerResourceMonitor>();
            services.AddSingleton<IServerResourceAggregator, ServerResourceAggregator>();
            services.AddSingleton<IGameServerResourceCollector, GameServerResourceCollectorService>();
            services.AddHostedService(sp => (GameServerResourceCollectorService)sp.GetRequiredService<IGameServerResourceCollector>());
            services.AddSingleton<IServerLogAggregator, ServerLogAggregator>();
            services.AddSingleton<IContainerAttachAggregator, ContainerAttachAggregator>();
            services.AddSingleton<IGameServerReadinessWatcherService, GameServerReadinessWatcherService>();

            return services;
        }
    }
}
