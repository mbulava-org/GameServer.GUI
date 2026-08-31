using GameServer.API.Data.V2;
using GameServer.API.Models.V2;
using GameServer.API.Repositories.V2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Testcontainers.MySql;

namespace GameServer.API.Tests.Repositories.V2;

public class GameTypeRepositoryMySqlTests : IAsyncLifetime
{
    private MySqlContainer? _container;
    private MySqlGameServerV2DbContext? _context;
    private GameTypeRepository? _repository;

    public async ValueTask InitializeAsync()
    {
        _container = new MySqlBuilder()
            .WithImage("mysql:8.4")
            .WithDatabase($"gameserver_v2_{Guid.NewGuid():N}")
            .WithUsername("gameserver")
            .WithPassword("P@ssw0rd123!")
            .Build();

        await _container.StartAsync();

        var optionsBuilder = new DbContextOptionsBuilder<MySqlGameServerV2DbContext>();
        GameServerV2DbContextFactory.ConfigureProvider(optionsBuilder, "mysql", _container.GetConnectionString());

        _context = new MySqlGameServerV2DbContext(optionsBuilder.Options);
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

    [Fact]
    public async Task InitializeDatabaseAsync_WhenUsingMySqlContainer_ShouldCreateSchemaAndRemainQueryable()
    {
        ArgumentNullException.ThrowIfNull(_repository);
        ArgumentNullException.ThrowIfNull(_context);

        await _repository.InitializeDatabaseAsync();
        await _repository.InitializeDatabaseAsync();

        var canConnect = await _context.Database.CanConnectAsync();
        var count = await _context.GameTypes.CountAsync();
        var gameTypesTableExists = (await _context.Database.SqlQueryRaw<int>("SELECT 1 AS `Value` FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'GameTypes' LIMIT 1").ToListAsync()).Count > 0;

        Assert.True(canConnect);
        Assert.True(gameTypesTableExists);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CreateAsync_WhenUsingMySqlContainer_ShouldPersistAggregate()
    {
        ArgumentNullException.ThrowIfNull(_repository);

        await _repository.InitializeDatabaseAsync();

        var gameType = new GameType
        {
            Key = "minecraft-mysql-v2",
            DisplayName = "Minecraft MySql V2",
            Description = "MySQL-backed repository test",
            Type = "docker",
            Revisions =
            [
                new GameTypeRevision
                {
                    ImageReference = "itzg/minecraft-server",
                    VersionTag = "latest",
                    ImageDigest = "sha256:mysql-test",
                    IsPublished = true,
                    Ports =
                    [
                        new GameTypePort { ContainerPort = 25565, Protocol = "tcp", AdvertisedPort = true, Description = "Game Port", DisplayOrder = 0 }
                    ],
                    Volumes =
                    [
                        new GameTypeVolume { Source = "data", Usage = "world", DisplayOrder = 0 }
                    ],
                    SettingDefinitions =
                    [
                        new GameTypeSettingDefinition
                        {
                            SettingKey = "EULA",
                            DefaultValue = "TRUE",
                            DisplayOrder = 0,
                            Metadata = new GameTypeSettingMetadata
                            {
                                DataType = "boolean",
                                IsRequired = true,
                                PortMappings = []
                            }
                        }
                    ]
                }
            ]
        };

        var created = await _repository.CreateAsync(gameType);
        var loaded = await _repository.GetByKeyAsync(gameType.Key);

        Assert.NotNull(loaded);
        Assert.Equal(created.Id, loaded.Id);
        Assert.Equal("docker", loaded.Type);
        Assert.Single(loaded.Revisions);
        Assert.Equal("itzg/minecraft-server", loaded.Revisions[0].ImageReference);
        Assert.Equal("latest", loaded.Revisions[0].VersionTag);
        Assert.True(loaded.Revisions[0].Ports[0].AdvertisedPort);
    }

    [Fact]
    public async Task InitializeDatabaseAsync_ShouldApplyAllMigrationsAndBeIdempotent()
    {
        ArgumentNullException.ThrowIfNull(_repository);
        ArgumentNullException.ThrowIfNull(_context);

        await _repository.InitializeDatabaseAsync();
        // Running twice must be a no-op once all migrations are recorded.
        await _repository.InitializeDatabaseAsync();

        var appliedMigrations = (await _context.Database.GetAppliedMigrationsAsync()).ToList();
        Assert.Equal(_context.Database.GetMigrations(), appliedMigrations);
        Assert.Empty(await _context.Database.GetPendingMigrationsAsync());

        // Mount type defaults come from the model's HasData seed, applied by the migrations.
        Assert.True(await _context.MountTypeConfigs.AnyAsync(x => x.Key == "volume"));
        Assert.True(await _context.MountTypeConfigs.AnyAsync(x => x.Key == "nfs"));
    }
}
