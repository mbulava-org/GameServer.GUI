using GameServer.API.Configurations;
using GameServer.API.Interfaces;
using GameServer.API.Services;
using GameServer.API.Services.V2;
using GameServer.API.Services.V2.MountTypeHandlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Deployment
{
    public static class DeploymentServiceExtensions
    {
        public static IServiceCollection AddDeploymentModule(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var portAllocationConfig = configuration.GetSection("PortAllocation").Get<PortAllocation>() ?? new PortAllocation();
            var rawReservedPorts = configuration["PortAllocation:ReservedPortRanges"] ?? configuration["PortAllocation__ReservedPortRanges"];
            if (!string.IsNullOrWhiteSpace(rawReservedPorts))
            {
                var rawTokens = rawReservedPorts.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                portAllocationConfig.ReservedPortRanges = (portAllocationConfig.ReservedPortRanges ?? Array.Empty<string>())
                    .Concat(rawTokens)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            var networkOptions = configuration.GetSection("NetworkOptions").Get<NetworkOptions>() ?? new NetworkOptions();

            services.AddSingleton(portAllocationConfig);
            services.AddSingleton(networkOptions);

            services.AddSingleton<PortAllocator>();
            services.AddSingleton<IServiceOperations, ServiceOperationsViaAgent>();

            services.AddScoped<GameServerValidationService>();
            services.AddScoped<GameServerSpecBuilder>();
            services.AddScoped<GameServerDeploymentService>();
            services.AddScoped<GameServerCommandService>();
            services.AddScoped<GameTypeCommandService>();
            services.AddScoped<IVolumeSetupResolver, VolumeSetupResolver>();
            services.AddScoped<IMountTypeHandler, VolumeMountTypeHandler>();
            services.AddScoped<IMountTypeHandler, NfsMountTypeHandler>();
            services.AddScoped<IMountTypeHandlerFactory, MountTypeHandlerFactory>();
            services.AddScoped<IGameServerFilesService, GameServerFilesService>();

            return services;
        }
    }
}
