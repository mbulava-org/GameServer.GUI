using GameServer.API.Configurations;
using GameServer.API.Interfaces;
using GameServer.API.Services;
using GameServer.Orchestration.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameServer.Orchestration
{
    public static class OrchestrationServiceExtensions
    {
        /// <summary>
        /// Registers HTTP-client implementations of <see cref="IAgentRegistry"/> and
        /// <see cref="INodeAgentDiscovery"/> that call the standalone
        /// <c>GameServer.Orchestration.Host</c> REST API. Use this in place of
        /// <see cref="AddOrchestrationModule"/> for hosts that only need read-only
        /// access to the orchestration state (e.g. GameServer.Monitoring.Host and
        /// GameServer.API when configured with <c>ModularHosting:UseHttpAgentRegistry=true</c>).
        /// </summary>
        public static IServiceCollection AddOrchestrationHttpClientModule(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<OrchestrationServiceOptions>(
                configuration.GetSection(OrchestrationServiceOptions.SectionName));

            services.AddHttpClient<IAgentRegistry, OrchestrationHttpAgentRegistry>((sp, http) =>
            {
                var opts = sp.GetRequiredService<IOptions<OrchestrationServiceOptions>>().Value;
                http.BaseAddress = new Uri(opts.BaseUrl);
            });
            services.AddHttpClient<INodeAgentDiscovery, OrchestrationHttpNodeAgentDiscovery>((sp, http) =>
            {
                var opts = sp.GetRequiredService<IOptions<OrchestrationServiceOptions>>().Value;
                http.BaseAddress = new Uri(opts.BaseUrl);
            });

            return services;
        }

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
            services.AddSingleton<ITerminalSessionNotifier, Services.SignalRTerminalSessionNotifier>();

            return services;
        }
    }
}
