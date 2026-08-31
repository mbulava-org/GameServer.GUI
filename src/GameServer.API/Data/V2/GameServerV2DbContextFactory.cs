using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MySql.Data.MySqlClient;
using Npgsql;
using System.Reflection;

namespace GameServer.API.Data.V2;

/// <summary>
/// Factory for creating V2 DbContext instances at design-time.
/// </summary>
public class GameServerV2DbContextFactory : IDesignTimeDbContextFactory<GameServerV2DbContext>
{
    /// <summary>
    /// The assembly that contains the generated EF Core migrations for the V2 relational providers.
    /// </summary>
    internal static readonly string MigrationsAssemblyName =
        typeof(GameServerV2DbContext).Assembly.GetName().Name!;

    public GameServerV2DbContext CreateDbContext(string[] args)
    {
        var provider = ResolveArgument(args, "--provider") ?? "sqlite";
        var connectionString = ResolveArgument(args, "--connection") ?? "Data Source=:memory:";

        // Design-time operations (e.g. `dotnet ef migrations add`) must never require a live
        // database connection, so connection validation is disabled here.
        switch (provider.Trim().ToLowerInvariant())
        {
            case "mysql":
            {
                var optionsBuilder = new DbContextOptionsBuilder<MySqlGameServerV2DbContext>();
                ConfigureProvider((DbContextOptionsBuilder)optionsBuilder, provider, connectionString, validateConnection: false);
                return new MySqlGameServerV2DbContext(optionsBuilder.Options);
            }

            case "postgres":
            case "postgresql":
            {
                var optionsBuilder = new DbContextOptionsBuilder<GameServerV2DbContext>();
                ConfigureProvider((DbContextOptionsBuilder)optionsBuilder, provider, connectionString, validateConnection: false);
                return new GameServerV2DbContext(optionsBuilder.Options);
            }

            default:
            {
                var optionsBuilder = new DbContextOptionsBuilder<SqliteGameServerV2DbContext>();
                ConfigureProvider((DbContextOptionsBuilder)optionsBuilder, "sqlite", connectionString, validateConnection: false);
                return new SqliteGameServerV2DbContext(optionsBuilder.Options);
            }
        }
    }

    internal static void ConfigureProvider(
        DbContextOptionsBuilder optionsBuilder,
        string provider,
        string connectionString,
        bool validateConnection = true)
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

                optionsBuilder.UseSqlite(
                    sqliteConnectionString,
                    x =>
                    {
                        x.MigrationsAssembly(MigrationsAssemblyName);
                        x.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                    });
                break;

            case "mysql":
                ConfigureMySqlProvider(optionsBuilder, connectionString, validateConnection);
                break;

            case "postgres":
            case "postgresql":
                ConfigurePostgreSqlProvider(optionsBuilder, connectionString);
                break;

            default:
                throw new NotSupportedException($"Unsupported V2 database provider '{provider}'. Supported providers: sqlite, mysql, postgresql.");
        }
    }

    private static void ConfigureMySqlProvider(DbContextOptionsBuilder optionsBuilder, string connectionString, bool validateConnection)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        var mySqlCS = new MySqlConnectionStringBuilder(connectionString)
        {
            Pooling = true,
            ConnectionTimeout = 30,
            DefaultCommandTimeout = 30,
        };

        if (validateConnection)
        {
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
        }

        optionsBuilder.UseMySQL(mySqlCS.ConnectionString, options =>
        {
            options.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
            options.MigrationsAssembly(MigrationsAssemblyName);
            options.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
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
            options.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
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
