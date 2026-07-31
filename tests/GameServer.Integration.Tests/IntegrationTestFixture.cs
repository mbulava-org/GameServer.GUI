using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

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
    private readonly string _v2DatabasePath;

    public IntegrationTestFactory()
    {
        // Set before the host starts so Program.cs reads it during service registration
        Environment.SetEnvironmentVariable("SKIP_DB_INIT", "true");
        _v2DatabasePath = Path.Combine(Path.GetTempPath(), $"gameserver-v2-integration-{Guid.NewGuid():N}.db");
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
                ["ConnectionStrings:GameServerV2Db"] = $"Data Source={_v2DatabasePath}",
            });
            Environment.SetEnvironmentVariable("SKIP_DB_INIT", "true");
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Create schema in the open connections before any test runs
        using var scope = host.Services.CreateScope();
        var sp = scope.ServiceProvider;

        sp.GetRequiredService<GameServer.Docker.Data.V2.GameServerV2DbContext>()
          .Database.EnsureCreated();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            if (File.Exists(_v2DatabasePath))
            {
                File.Delete(_v2DatabasePath);
            }
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
