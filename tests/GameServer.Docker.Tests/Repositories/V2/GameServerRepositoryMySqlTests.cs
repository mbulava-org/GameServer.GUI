using GameServer.Docker.Data.V2;
using GameServer.Docker.Repositories.V2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Testcontainers.MySql;
using GameServerModel = GameServer.Docker.Models.V2.GameServer;
using GameServerSettingModel = GameServer.Docker.Models.V2.GameServerSetting;

namespace GameServer.Docker.Tests.Repositories.V2;

public class GameServerRepositoryMySqlTests : IAsyncLifetime
{
    private MySqlContainer? _container;
    private MySqlGameServerV2DbContext? _context;
    private GameTypeRepository? _gameTypeRepository;
    private GameServerRepository? _gameServerRepository;
    private int _revisionId;

    public async Task InitializeAsync()
    {
        _container = new MySqlBuilder()
            .WithImage("mysql:8.4")
            .WithDatabase($"gameserver_servers_{Guid.NewGuid():N}")
            .WithUsername("gameserver")
            .WithPassword("P@ssw0rd123!")
            .Build();

        await _container.StartAsync();

        var optionsBuilder = new DbContextOptionsBuilder<MySqlGameServerV2DbContext>();
        GameServerV2DbContextFactory.ConfigureProvider(optionsBuilder, "mysql", _container.GetConnectionString());

        _context = new MySqlGameServerV2DbContext(optionsBuilder.Options);
        _gameTypeRepository = new GameTypeRepository(_context, Mock.Of<ILogger<GameTypeRepository>>());
        _gameServerRepository = new GameServerRepository(_context, Mock.Of<ILogger<GameServerRepository>>());

        await _gameTypeRepository.InitializeDatabaseAsync();

        var gameType = await _gameTypeRepository.CreateAsync(new GameServer.Docker.Models.V2.GameType
        {
            Key = "mysql-server-test",
            DisplayName = "MySql Server Test",
            Type = "docker",
            Revisions =
            [
                new GameServer.Docker.Models.V2.GameTypeRevision
                {
                    ImageReference = "repo/test",
                    VersionTag = "latest",
                    IsPublished = true,
                    Ports =
                    [
                        new GameServer.Docker.Models.V2.GameTypePort { ContainerPort = 25565, Protocol = "tcp", AdvertisedPort = true }
                    ]
                }
            ]
        });

        _revisionId = gameType.Revisions.Single().Id;
    }

    public async Task DisposeAsync()
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

    [Fact]
    public async Task CreateAsync_WhenUsingMySqlContainer_ShouldPersistAggregate()
    {
        ArgumentNullException.ThrowIfNull(_gameServerRepository);

        var server = new GameServerModel
        {
            ServerId = "server-mysql-001",
            Name = "Alpha",
            GameTypeRevisionId = _revisionId,
            ServiceName = "alpha-service",
            Status = "Created",
            Settings =
            [
                new GameServerSettingModel { SettingKey = "EULA", Value = "TRUE" }
            ]
        };

        var created = await _gameServerRepository.CreateAsync(server);
        var loaded = await _gameServerRepository.GetByServerIdAsync(server.ServerId);

        Assert.NotNull(loaded);
        Assert.Equal(created.Id, loaded.Id);
        Assert.Equal(_revisionId, loaded.GameTypeRevisionId);
        Assert.Single(loaded.Settings);
    }

    [Fact]
    public async Task DeleteAsync_WhenUsingMySqlContainerAndSoftDeleteRequested_ShouldMarkEntityDeleted()
    {
        ArgumentNullException.ThrowIfNull(_gameServerRepository);

        await _gameServerRepository.CreateAsync(new GameServerModel
        {
            ServerId = "server-mysql-002",
            Name = "Bravo",
            GameTypeRevisionId = _revisionId,
            ServiceName = "bravo-service",
            Status = "Created"
        });

        await _gameServerRepository.DeleteAsync("server-mysql-002", softDelete: true);

        var deleted = await _gameServerRepository.GetByServerIdAsync("server-mysql-002");

        Assert.NotNull(deleted);
        Assert.True(deleted!.IsDeleted);
    }
}
