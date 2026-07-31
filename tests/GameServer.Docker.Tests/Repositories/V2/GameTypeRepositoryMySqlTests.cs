using GameServer.Docker.Data.V2;
using GameServer.Docker.Models.V2;
using GameServer.Docker.Repositories.V2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Testcontainers.MySql;

namespace GameServer.Docker.Tests.Repositories.V2;

public class GameTypeRepositoryMySqlTests : IAsyncLifetime
{
    private MySqlContainer? _container;
    private MySqlGameServerV2DbContext? _context;
    private GameTypeRepository? _repository;

    public async Task InitializeAsync()
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
    public async Task InitializeDatabaseAsync_WhenPreMigrationLegacySchemaExists_ShouldReconcileToBaselineAndAllowCreates()
    {
        ArgumentNullException.ThrowIfNull(_repository);
        ArgumentNullException.ThrowIfNull(_context);

        // Simulate a database created before EF migrations were adopted: only the legacy GameTypes table
        // with an ImageReference column and no __EFMigrationsHistory table.
        await _context.Database.ExecuteSqlRawAsync(
            "CREATE TABLE `GameTypes` (`Id` int NOT NULL AUTO_INCREMENT, `Key` varchar(200) NOT NULL, `DisplayName` varchar(200) NOT NULL, `Description` longtext NULL, `ImageReference` varchar(500) NOT NULL, `ThumbnailUrl` longtext NULL, `DocumentationUrl` longtext NULL, `IsActive` tinyint(1) NOT NULL, `CurrentRevisionId` int NULL, `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6), `UpdatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6), PRIMARY KEY (`Id`), UNIQUE KEY `IX_GameTypes_Key` (`Key`));");
        await _context.Database.ExecuteSqlRawAsync(
            "CREATE TABLE `GameTypeRevisions` (`Id` int NOT NULL AUTO_INCREMENT, `GameTypeId` int NOT NULL, `VersionTag` varchar(200) NOT NULL, `ImageDigest` longtext NULL, `EnableTTY` tinyint(1) NOT NULL, `Notes` longtext NULL, `IsPublished` tinyint(1) NOT NULL, `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6), PRIMARY KEY (`Id`), KEY `IX_GameTypeRevisions_GameTypeId` (`GameTypeId`));");
        await _context.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX `IX_GameTypeRevisions_GameTypeId_VersionTag` ON `GameTypeRevisions` (`GameTypeId`, `VersionTag`);");
        await _context.Database.ExecuteSqlRawAsync(
            "INSERT INTO `GameTypes` (`Key`, `DisplayName`, `ImageReference`, `IsActive`) VALUES ('minecraft', 'Minecraft', 'itzg/minecraft-server', 1);");

        await _repository.InitializeDatabaseAsync();
        // Running twice must be a no-op after the baseline is recorded.
        await _repository.InitializeDatabaseAsync();

        var hasLegacyImageReferenceColumn = await _context.Database
            .SqlQueryRaw<int>("SELECT 1 AS `Value` FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'GameTypes' AND column_name = 'ImageReference' LIMIT 1")
            .AnyAsync();

        var revisionHasImageReference = await _context.Database
            .SqlQueryRaw<int>("SELECT 1 AS `Value` FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'GameTypeRevisions' AND column_name = 'ImageReference' LIMIT 1")
            .AnyAsync();

        var preservedGameTypeCount = await _context.GameTypes.CountAsync();

        Assert.False(hasLegacyImageReferenceColumn);
        Assert.True(revisionHasImageReference);
        Assert.Equal(1, preservedGameTypeCount);
    }
}
