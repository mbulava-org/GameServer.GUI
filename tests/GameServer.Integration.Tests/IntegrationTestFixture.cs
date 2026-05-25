using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace GameServer.Integration.Tests;

/// <summary>
/// Custom WebApplicationFactory that:
/// 1. Replaces Serilog bootstrap logger to avoid "logger already frozen" errors.
/// 2. Replaces EF Core DbContext registrations to use open SQLite connections so that
///    in-process tests do not need a running database server.
/// 3. Skips the background DatabaseInitializationService to prevent StopApplication() calls.
/// </summary>
public class IntegrationTestFactory : WebApplicationFactory<GameServer.Docker.Program>
{
    // Keep open connections for the lifetime of the factory so the in-memory databases persist
    private readonly SqliteConnection _legacyConnection;
    private readonly SqliteConnection _v2Connection;

    public IntegrationTestFactory()
    {
        // Set before the host starts so Program.cs reads it during service registration
        Environment.SetEnvironmentVariable("SKIP_DB_INIT", "true");

        _legacyConnection = new SqliteConnection("Data Source=:memory:");
        _legacyConnection.Open();

        _v2Connection = new SqliteConnection("Data Source=:memory:");
        _v2Connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
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
                // Force V2 to use SQLite so Npgsql is never selected
                ["V2Database:Provider"] = "sqlite",
                ["V2Database:ConnectionStringName"] = "GameServerV2Db",
                ["ConnectionStrings:GameServerV2Db"] = "Data Source=:memory:",
            });
            Environment.SetEnvironmentVariable("SKIP_DB_INIT", "true");
        });

        builder.ConfigureServices(services =>
        {
            // Remove ALL descriptors related to GameServerDbContext
            var legacyDescriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<GameServer.Docker.Data.GameServerDbContext>)
                         || d.ServiceType == typeof(GameServer.Docker.Data.GameServerDbContext))
                .ToList();
            foreach (var d in legacyDescriptors) services.Remove(d);

            // Remove ALL descriptors related to GameServerV2DbContext
            var v2Descriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<GameServer.Docker.Data.V2.GameServerV2DbContext>)
                         || d.ServiceType == typeof(GameServer.Docker.Data.V2.GameServerV2DbContext))
                .ToList();
            foreach (var d in v2Descriptors) services.Remove(d);

            // Register isolated SQLite backed by an open connection (preserves in-memory schema)
            services.AddDbContext<GameServer.Docker.Data.GameServerDbContext>(options =>
                options.UseSqlite(_legacyConnection)
                       .EnableServiceProviderCaching(false));

            services.AddDbContext<GameServer.Docker.Data.V2.GameServerV2DbContext>(options =>
                options.UseSqlite(_v2Connection)
                       .EnableServiceProviderCaching(false));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Create schema in the open connections before any test runs
        using var scope = host.Services.CreateScope();
        var sp = scope.ServiceProvider;

        sp.GetRequiredService<GameServer.Docker.Data.GameServerDbContext>()
          .Database.EnsureCreated();
        sp.GetRequiredService<GameServer.Docker.Data.V2.GameServerV2DbContext>()
          .Database.EnsureCreated();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _legacyConnection.Dispose();
            _v2Connection.Dispose();
        }
    }
}

/// <summary>
/// Shared collection fixture — all integration test classes share one factory instance,
/// preventing the Serilog "logger already frozen" error on second instantiation.
/// </summary>
[CollectionDefinition("Integration Tests")]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFactory>
{
}
