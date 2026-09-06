using GameServer.API.Configurations;
using GameServer.API.Interfaces;
using GameServer.API.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameServer.Orchestration
{
    public static class OrchestrationServiceExtensions
    {
        public static IServiceCollection AddOrchestrationModule(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var nodeAgentOptions = configuration.GetSection("NodeAgentOptions").Get<NodeAgentOptions>() ?? new NodeAgentOptions();
            var udpAgentDiscoveryOptions = configuration.GetSection(UdpAgentDiscoveryOptions.SectionName).Get<UdpAgentDiscoveryOptions>() ?? new UdpAgentDiscoveryOptions();

            services.AddSingleton(nodeAgentOptions);
            services.AddSingleton(udpAgentDiscoveryOptions);

            services.AddSingleton<IAgentRegistry, AgentRegistryService>();
            services.AddSingleton<IUdpAgentRegistry, UdpAgentRegistryService>();
            services.AddHostedService<UdpAgentAnnouncementListenerService>();

            services.AddHttpClient();
            services.AddSingleton<NodeAgentDiscoveryService>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<NodeAgentDiscoveryService>>();
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var agentOptions = sp.GetRequiredService<NodeAgentOptions>();
                var agentRegistry = sp.GetRequiredService<IAgentRegistry>();
                var udpAgentRegistry = sp.GetRequiredService<IUdpAgentRegistry>();

                return new NodeAgentDiscoveryService(
                    logger,
                    httpClientFactory,
                    sp,
                    agentOptions,
                    agentRegistry,
                    udpAgentRegistry);
            });
            services.AddSingleton<INodeAgentDiscovery>(sp => sp.GetRequiredService<NodeAgentDiscoveryService>());
            services.AddHostedService(sp => sp.GetRequiredService<NodeAgentDiscoveryService>());

            services.AddSingleton<NodeAgentClient>();
            services.AddHostedService<AgentShutdownNotificationService>();

            services.AddSingleton<TerminalSessionManager>();

            return services;
        }
    }
}
