using GameServer.API.Data.V2;
using GameServer.API.Repositories.V2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Testcontainers.PostgreSql;

namespace GameServer.API.Tests.Repositories.V2;

// Ignored for now: the V2 PostgreSQL schema is deployed externally via pgPacTool, so this
// suite needs a provisioned schema in addition to a Postgres container. Re-enable once the
// schema deployment is wired into the test fixture.
[Trait("Category", "PostgreSql")]
public class GameTypeRepositoryPostgreSqlTests : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private GameServerV2DbContext? _context;
    private GameTypeRepository? _repository;

    public async ValueTask InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:17")
            .WithDatabase($"gameserver_v2_{Guid.NewGuid():N}")
            .WithUsername("gameserver")
            .WithPassword("P@ssw0rd123!")
            .Build();

        await _container.StartAsync();

        var optionsBuilder = new DbContextOptionsBuilder<GameServerV2DbContext>();
        GameServerV2DbContextFactory.ConfigureProvider(optionsBuilder, "postgresql", _container.GetConnectionString());

        _context = new GameServerV2DbContext(optionsBuilder.Options);
        _repository = new GameTypeRepository(_context, Mock.Of<ILogger<GameTypeRepository>>());
    }

    public async ValueTask DisposeAsync()
    {
        if (_context is not null)
        {
            await _context.DisposeAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    [Fact(Skip = "Ignored for now - PostgreSQL V2 schema must be deployed externally via pgPacTool before this can run.")]
    public async Task InitializeDatabaseAsync_WhenSchemaHasNotBeenDeployed_ShouldThrowGuidance()
    {
        ArgumentNullException.ThrowIfNull(_repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.InitializeDatabaseAsync());

        Assert.Contains("pgPacTool", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Deploy-V2PostgresDatabase.ps1", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}

