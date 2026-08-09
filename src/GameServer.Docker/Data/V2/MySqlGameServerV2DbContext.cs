using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GameServer.Docker.Data.V2;

/// <summary>
/// MySQL-specific V2 DbContext. Exists so the MySQL migration set is generated and applied
/// against a dedicated context type (EF Core allows only one model snapshot per context type).
/// The model itself is inherited from <see cref="GameServerV2DbContext"/>.
/// </summary>
public class MySqlGameServerV2DbContext : GameServerV2DbContext
{
    public MySqlGameServerV2DbContext(DbContextOptions<MySqlGameServerV2DbContext> options)
        : base(options)
    {
    }
}

/// <summary>
/// Design-time factory used by the EF Core tools to scaffold and manage the MySQL migration set.
/// The dummy connection string is never opened because connection validation is disabled.
/// </summary>
public sealed class MySqlGameServerV2DbContextFactory : IDesignTimeDbContextFactory<MySqlGameServerV2DbContext>
{
    public MySqlGameServerV2DbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MySqlGameServerV2DbContext>();
        GameServerV2DbContextFactory.ConfigureProvider(
            optionsBuilder,
            "mysql",
            "server=localhost;port=3306;database=gameserver_v2_design;user=root;password=design",
            validateConnection: false);
        return new MySqlGameServerV2DbContext(optionsBuilder.Options);
    }
}
