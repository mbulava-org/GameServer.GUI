using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using GameServer.API.Data.V2;
using GameServer.API.Services;
using Moq;

namespace GameServer.Integration.Tests;

/// <summary>
/// Custom WebApplicationFactory that:
/// 1. Replaces Serilog bootstrap logger to avoid "logger already frozen" errors.
/// 2. Replaces EF Core DbContext registrations to use SQLite so that
///    in-process tests do not need a running PostgreSQL server.
/// 3. Skips the background DatabaseInitializationService and sets readiness gate.
/// </summary>
public class IntegrationTestFactory : WebApplicationFactory<GameServer.API.Program>
{
    private readonly string _v2DatabasePath;

    public IntegrationTestFactory()
    {
        _v2DatabasePath = Path.Combine(Path.GetTempPath(), $"gameserver-v2-integration-{Guid.NewGuid():N}.db");
        Environment.SetEnvironmentVariable("SKIP_DB_INIT", "true");
        Environment.SetEnvironmentVariable("V2Database:Provider", "sqlite");
        Environment.SetEnvironmentVariable("V2Database__Provider", "sqlite");
        Environment.SetEnvironmentVariable("V2Database:ConnectionStringName", "GameServerV2Db");
        Environment.SetEnvironmentVariable("V2Database__ConnectionStringName", "GameServerV2Db");
        Environment.SetEnvironmentVariable("ConnectionStrings:GameServerV2Db", $"Data Source={_v2DatabasePath}");
        Environment.SetEnvironmentVariable("ConnectionStrings__GameServerV2Db", $"Data Source={_v2DatabasePath}");
        Environment.SetEnvironmentVariable("ConnectionStrings:GameServerV2PostgresDb", $"Data Source={_v2DatabasePath}");
        Environment.SetEnvironmentVariable("ConnectionStrings__GameServerV2PostgresDb", $"Data Source={_v2DatabasePath}");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("SKIP_DB_INIT", "true");
        builder.UseSetting("V2Database:Provider", "sqlite");
        builder.UseSetting("V2Database:ConnectionStringName", "GameServerV2Db");
        builder.UseSetting("ConnectionStrings:GameServerV2Db", $"Data Source={_v2DatabasePath}");
        builder.UseSetting("ConnectionStrings:GameServerV2PostgresDb", $"Data Source={_v2DatabasePath}");

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });

        builder.ConfigureAppConfiguration((ctx, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SKIP_DB_INIT"] = "true",
                ["V2Database:Provider"] = "sqlite",
                ["V2Database:ConnectionStringName"] = "GameServerV2Db",
                ["ConnectionStrings:GameServerV2Db"] = $"Data Source={_v2DatabasePath}",
                ["ConnectionStrings:GameServerV2PostgresDb"] = $"Data Source={_v2DatabasePath}",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove any existing DbContext options (e.g. Postgres) and register SQLite
            services.RemoveAll<DbContextOptions<GameServerV2DbContext>>();
            services.RemoveAll<DbContextOptions<SqliteGameServerV2DbContext>>();
            services.RemoveAll<GameServerV2DbContext>();
            services.RemoveAll<SqliteGameServerV2DbContext>();

            services.AddDbContext<SqliteGameServerV2DbContext>(options =>
            {
                options.UseSqlite($"Data Source={_v2DatabasePath}");
                options.EnableServiceProviderCaching(false);
            });
            services.AddScoped<GameServerV2DbContext>(sp => sp.GetRequiredService<SqliteGameServerV2DbContext>());

            var mockServiceOps = new Moq.Mock<GameServer.API.Interfaces.IServiceOperations>();
            mockServiceOps.Setup(s => s.ListServicesAsync(Moq.It.IsAny<string?>(), Moq.It.IsAny<string?>(), Moq.It.IsAny<CancellationToken>()))
                .ReturnsAsync((IList<Docker.DotNet.Models.SwarmService>)new List<Docker.DotNet.Models.SwarmService>());
            services.RemoveAll<GameServer.API.Interfaces.IServiceOperations>();
            services.AddSingleton(mockServiceOps.Object);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Create schema in SQLite database before any test runs
        using var scope = host.Services.CreateScope();
        var sp = scope.ServiceProvider;

        sp.GetRequiredService<GameServerV2DbContext>()
          .Database.EnsureCreated();

        sp.GetService<IDatabaseReadinessGate>()?.MarkReady();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            if (File.Exists(_v2DatabasePath))
            {
                try { File.Delete(_v2DatabasePath); } catch { }
            }
        }
    }
}

/// <summary>
/// Shared collection fixture — all integration test classes share one factory instance.
/// </summary>
[CollectionDefinition("Integration Tests")]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFactory>
{
}
