using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MySql.Data.MySqlClient;
using Npgsql;
using System.Reflection;

namespace GameServer.Docker.Data.V2;

/// <summary>
/// Factory for creating V2 DbContext instances at design-time.
/// </summary>
public class GameServerV2DbContextFactory : IDesignTimeDbContextFactory<GameServerV2DbContext>
{
    public GameServerV2DbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<GameServerV2DbContext>();
        var provider = ResolveArgument(args, "--provider") ?? "sqlite";
        var connectionString = ResolveArgument(args, "--connection") ?? "Data Source=:memory:";

        // Use a temporary in-memory SQLite database by default for design-time operations.
        // PostgreSQL and MySQL can still be selected explicitly through arguments when needed.
        ConfigureProvider(optionsBuilder, provider, connectionString);
        return new GameServerV2DbContext(optionsBuilder.Options);
    }

    internal static void ConfigureProvider(DbContextOptionsBuilder optionsBuilder, string provider, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new ArgumentException("V2 database provider is required.", nameof(provider));
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("V2 database connection string is required.", nameof(connectionString));
        }

        switch (provider.Trim().ToLowerInvariant())
        {
            case "sqlite":
                var sqliteConnectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString)
                {
                    Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
                    Cache = Microsoft.Data.Sqlite.SqliteCacheMode.Shared,
                    Pooling = true
                }.ToString();

                optionsBuilder.UseSqlite(sqliteConnectionString);
                break;

            case "mysql":
                ConfigureMySqlProvider(optionsBuilder, connectionString);
                break;

            case "postgres":
            case "postgresql":
                ConfigurePostgreSqlProvider(optionsBuilder, connectionString);
                break;

            default:
                throw new NotSupportedException($"Unsupported V2 database provider '{provider}'. Supported providers: sqlite, mysql, postgresql.");
        }
    }

    private static void ConfigureMySqlProvider(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        var mySqlCS = new MySqlConnectionStringBuilder(connectionString)
        {
            Pooling = true,
            ConnectionTimeout = 30,
            DefaultCommandTimeout = 30,
        };

        try
        {
            // Test the connection string by opening a connection.
            using var testConnection = new MySqlConnection(mySqlCS.ConnectionString);
            testConnection.Open();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to connect to MySQL database with the provided connection string: \"{mySqlCS.ConnectionString}\". Please verify the connection details.", ex);
        }
        optionsBuilder.UseMySQL(mySqlCS.ConnectionString, options =>
        {
            options.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        });
    }

    private static void ConfigurePostgreSqlProvider(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        var postgreSqlConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = true,
            Timeout = 30,
            CommandTimeout = 30
        }.ConnectionString;

        optionsBuilder.UseNpgsql(postgreSqlConnectionString, options =>
        {
            options.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null);
        });
    }

    private static string? ResolveArgument(IReadOnlyList<string> args, string name)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }
}
