using GameServer.API.Configurations;
using GameServer.API.Data.V2;
using GameServer.API.Interfaces;
using GameServer.API.Repositories.V2;
using GameServer.API.Services;
using GameServer.API.Services.V2.Detection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameServer.Catalog
{
    public static class CatalogServiceExtensions
    {
        public static IServiceCollection AddCatalogModule(
            this IServiceCollection services,
            IConfiguration configuration,
            IWebHostEnvironment? environment = null,
            bool skipDbInit = false)
        {
            services.Configure<V2DatabaseOptions>(configuration.GetSection(V2DatabaseOptions.SectionName));

            // Database readiness gate
            services.AddSingleton<IDatabaseReadinessGate, DatabaseReadinessGate>();

            // V2 Database context configuration
            var v2Options = configuration
                .GetSection(V2DatabaseOptions.SectionName)
                .Get<V2DatabaseOptions>() ?? new V2DatabaseOptions();

            var provider = (v2Options.Provider ?? "sqlite").Trim().ToLowerInvariant();
            var defaultConnectionName = provider switch
            {
                "postgres" or "postgresql" => "GameServerV2PostgresDb",
                "mysql" => "GameServerV2MySqlDb",
                _ => "GameServerV2Db"
            };
            var connectionName = string.IsNullOrWhiteSpace(v2Options.ConnectionStringName)
                ? defaultConnectionName
                : v2Options.ConnectionStringName;

            var v2ConnectionString = configuration.GetConnectionString(connectionName)
                ?? configuration.GetConnectionString(defaultConnectionName)
                ?? configuration.GetConnectionString("GameServerV2Db")
                ?? "Data Source=./data/gameserver-v2.db";

            void ConfigureV2Options(DbContextOptionsBuilder options)
            {
                GameServerV2DbContextFactory.ConfigureProvider(
                    options,
                    provider,
                    v2ConnectionString);

                if (environment != null && environment.IsDevelopment())
                {
                    options.EnableSensitiveDataLogging();
                }

                options.EnableServiceProviderCaching(false);
            }

            switch (provider)
            {
                case "mysql":
                    services.AddDbContext<MySqlGameServerV2DbContext>((_, options) => ConfigureV2Options(options));
                    services.AddScoped<GameServerV2DbContext>(sp => sp.GetRequiredService<MySqlGameServerV2DbContext>());
                    break;

                case "postgres":
                case "postgresql":
                    services.AddDbContext<GameServerV2DbContext>((_, options) => ConfigureV2Options(options));
                    break;

                default:
                    services.AddDbContext<SqliteGameServerV2DbContext>((_, options) => ConfigureV2Options(options));
                    services.AddScoped<GameServerV2DbContext>(sp => sp.GetRequiredService<SqliteGameServerV2DbContext>());
                    break;
            }

            // Memory Cache & V2 Repositories
            services.AddMemoryCache();
            services.AddScoped<IGameTypeRepository, GameTypeRepository>();
            services.AddScoped<IGameServerRepository, GameServerRepository>();
            services.AddScoped<IGameServerResourceUtilizationRepository, GameServerResourceUtilizationRepository>();
            services.AddScoped<IMountTypeConfigRepository, MountTypeConfigRepository>();

            // GameTypeSetupDetectionService
            services.AddScoped<GameTypeSetupDetectionService>(sp =>
                new GameTypeSetupDetectionService(
                    sp.GetRequiredService<IGameTypeRepository>(),
                    sp.GetRequiredService<IAgentRegistry>(),
                    sp.GetRequiredService<IHttpClientFactory>(),
                    sp.GetRequiredService<ILogger<GameTypeSetupDetectionService>>()));

            if (!skipDbInit)
            {
                services.AddHostedService<DatabaseInitializationService>();
            }

            return services;
        }
    }
}
