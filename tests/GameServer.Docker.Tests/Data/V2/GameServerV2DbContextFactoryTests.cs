using GameServer.Docker.Data.V2;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Docker.Tests.Data.V2;

public class GameServerV2DbContextFactoryTests
{
    [Fact]
    public void ConfigureProvider_WhenProviderIsPostgreSql_ShouldUseNpgsql()
    {
        var optionsBuilder = new DbContextOptionsBuilder<GameServerV2DbContext>();

        GameServerV2DbContextFactory.ConfigureProvider(
            optionsBuilder,
            "postgresql",
            "Host=localhost;Database=gameserver-v2;Username=postgres;Password=postgres");

        using var context = new GameServerV2DbContext(optionsBuilder.Options);

        Assert.Contains("Npgsql", context.Database.ProviderName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConfigureProvider_WhenProviderIsUnsupported_ShouldListPostgreSqlInMessage()
    {
        var optionsBuilder = new DbContextOptionsBuilder<GameServerV2DbContext>();

        var exception = Assert.Throws<NotSupportedException>(() =>
            GameServerV2DbContextFactory.ConfigureProvider(optionsBuilder, "oracle", "Data Source=ignored"));

        Assert.Contains("postgresql", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
