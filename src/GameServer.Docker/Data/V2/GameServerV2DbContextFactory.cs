using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
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
        // MySQL can still be selected explicitly through arguments when needed.
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

            default:
                throw new NotSupportedException($"Unsupported V2 database provider '{provider}'. Supported providers: sqlite, mysql.");
        }
    }

    private static void ConfigureMySqlProvider(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        var extensionType = Type.GetType("Microsoft.EntityFrameworkCore.MySQLDbContextOptionsExtensions, MySql.EntityFrameworkCore")
            ?? Type.GetType("Microsoft.EntityFrameworkCore.MySqlDbContextOptionsExtensions, MySql.EntityFrameworkCore");

        if (extensionType is null)
        {
            throw new InvalidOperationException("The MySQL EF Core provider assembly could not be loaded.");
        }

        var useMySqlMethod = extensionType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(method =>
            {
                if (!string.Equals(method.Name, "UseMySQL", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(method.Name, "UseMySql", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var parameters = method.GetParameters();
                return parameters.Length >= 2 &&
                       typeof(DbContextOptionsBuilder).IsAssignableFrom(parameters[0].ParameterType) &&
                       parameters[1].ParameterType == typeof(string);
            });

        if (useMySqlMethod is null)
        {
            throw new InvalidOperationException("A compatible UseMySql/UseMySQL method was not found in the MySQL EF Core provider.");
        }

        var parameters = useMySqlMethod.GetParameters();
        var invokeArguments = new object?[parameters.Length];
        invokeArguments[0] = optionsBuilder;
        invokeArguments[1] = connectionString;

        for (var index = 2; index < parameters.Length; index++)
        {
            invokeArguments[index] = parameters[index].HasDefaultValue ? parameters[index].DefaultValue : null;
        }

        useMySqlMethod.Invoke(null, invokeArguments);
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
