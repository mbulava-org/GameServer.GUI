using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GameServer.Docker.Data.V2;

/// <summary>
/// SQLite-specific V2 DbContext. Exists so the SQLite migration set is generated and applied
/// against a dedicated context type (EF Core allows only one model snapshot per context type).
/// The model itself is inherited from <see cref="GameServerV2DbContext"/>.
/// </summary>
public class SqliteGameServerV2DbContext : GameServerV2DbContext
{
    public SqliteGameServerV2DbContext(DbContextOptions<SqliteGameServerV2DbContext> options)
        : base(options)
    {
    }
}

/// <summary>
/// Design-time factory used by the EF Core tools to scaffold and manage the SQLite migration set.
/// </summary>
public sealed class SqliteGameServerV2DbContextFactory : IDesignTimeDbContextFactory<SqliteGameServerV2DbContext>
{
    public SqliteGameServerV2DbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SqliteGameServerV2DbContext>();
        GameServerV2DbContextFactory.ConfigureProvider(
            optionsBuilder,
            "sqlite",
            "Data Source=:memory:",
            validateConnection: false);
        return new SqliteGameServerV2DbContext(optionsBuilder.Options);
    }
}
