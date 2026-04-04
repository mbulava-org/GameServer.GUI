using GameServer.Docker.Data.V2;
using GameServer.Docker.Models.V2;
using GameServer.Docker.Repositories.V2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace GameServer.Docker.Tests.Repositories.V2;

public class GameTypeRepositoryTests : IDisposable
{
    private readonly GameServerV2DbContext _context;
    private readonly GameTypeRepository _repository;

    public GameTypeRepositoryTests()
    {
        var dbPath = $":memory:_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<GameServerV2DbContext>()
            .UseSqlite($"DataSource={dbPath};Mode=Memory;Cache=Shared")
            .Options;

        _context = new GameServerV2DbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _repository = new GameTypeRepository(_context, Mock.Of<ILogger<GameTypeRepository>>());
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public async Task InitializeDatabaseAsync_WhenNoMigrationsExist_ShouldEnsureSchemaAndRemainQueryable()
    {
        var dbPath = $":memory:_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<GameServerV2DbContext>()
            .UseSqlite($"DataSource={dbPath};Mode=Memory;Cache=Shared")
            .Options;

        await using var context = new GameServerV2DbContext(options);
        await context.Database.OpenConnectionAsync();

        var repository = new GameTypeRepository(context, Mock.Of<ILogger<GameTypeRepository>>());

        await repository.InitializeDatabaseAsync();

        var count = await context.GameTypes.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task InitializeDatabaseAsync_WhenLegacySchemaExists_ShouldUpgradeExistingRows()
    {
        var dbPath = $":memory:_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<GameServerV2DbContext>()
            .UseSqlite($"DataSource={dbPath};Mode=Memory;Cache=Shared")
            .Options;

        await using var context = new GameServerV2DbContext(options);
        await context.Database.OpenConnectionAsync();

        await context.Database.ExecuteSqlRawAsync(
            "CREATE TABLE \"GameTypes\" (\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_GameTypes\" PRIMARY KEY AUTOINCREMENT, \"Key\" TEXT NOT NULL, \"DisplayName\" TEXT NOT NULL, \"Description\" TEXT NULL, \"ImageReference\" TEXT NOT NULL, \"ThumbnailUrl\" TEXT NULL, \"DocumentationUrl\" TEXT NULL, \"IsActive\" INTEGER NOT NULL, \"CurrentRevisionId\" INTEGER NULL, \"CreatedAt\" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, \"UpdatedAt\" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);");
        await context.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX \"IX_GameTypes_Key\" ON \"GameTypes\" (\"Key\");");
        await context.Database.ExecuteSqlRawAsync("CREATE INDEX \"IX_GameTypes_IsActive\" ON \"GameTypes\" (\"IsActive\");");
        await context.Database.ExecuteSqlRawAsync(
            "CREATE TABLE \"GameTypeRevisions\" (\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_GameTypeRevisions\" PRIMARY KEY AUTOINCREMENT, \"GameTypeId\" INTEGER NOT NULL, \"VersionTag\" TEXT NOT NULL, \"ImageDigest\" TEXT NULL, \"EnableTTY\" INTEGER NOT NULL, \"Notes\" TEXT NULL, \"IsPublished\" INTEGER NOT NULL, \"CreatedAt\" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, CONSTRAINT \"FK_GameTypeRevisions_GameTypes_GameTypeId\" FOREIGN KEY (\"GameTypeId\") REFERENCES \"GameTypes\" (\"Id\") ON DELETE CASCADE);");
        await context.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX \"IX_GameTypeRevisions_GameTypeId_VersionTag\" ON \"GameTypeRevisions\" (\"GameTypeId\", \"VersionTag\");");
        await context.Database.ExecuteSqlRawAsync("INSERT INTO \"GameTypes\" (\"Id\", \"Key\", \"DisplayName\", \"Description\", \"ImageReference\", \"ThumbnailUrl\", \"DocumentationUrl\", \"IsActive\", \"CurrentRevisionId\", \"CreatedAt\", \"UpdatedAt\") VALUES (1, 'minecraft', 'Minecraft', 'Legacy schema row', 'itzg/minecraft-server', NULL, NULL, 1, 10, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);");
        await context.Database.ExecuteSqlRawAsync("INSERT INTO \"GameTypeRevisions\" (\"Id\", \"GameTypeId\", \"VersionTag\", \"ImageDigest\", \"EnableTTY\", \"Notes\", \"IsPublished\", \"CreatedAt\") VALUES (10, 1, 'latest', 'sha256:test', 1, 'legacy revision', 1, CURRENT_TIMESTAMP);");

        var repository = new GameTypeRepository(context, Mock.Of<ILogger<GameTypeRepository>>());

        await repository.InitializeDatabaseAsync();

        var upgradedGameType = await context.GameTypes.SingleAsync(x => x.Key == "minecraft");
        var upgradedRevision = await context.GameTypeRevisions.SingleAsync(x => x.GameTypeId == upgradedGameType.Id);

        Assert.Equal("docker", upgradedGameType.Type);
        Assert.Equal("itzg/minecraft-server", upgradedRevision.ImageReference);

        var migrationId = await context.Database.SqlQueryRaw<string>("SELECT \"MigrationId\" AS \"Value\" FROM \"__EFMigrationsHistory\"").SingleAsync();
        Assert.Equal("20260404190753_RefactorV2GameTypeTypeAndRevisionImageReference", migrationId);
    }

    [Fact]
    public async Task CreateAsync_WhenAggregateIncludesRevision_ShouldRoundTripAggregate()
    {
        var gameType = new GameType
        {
            Key = "minecraft-v2",
            DisplayName = "Minecraft V2",
            Description = "Versioned test game type",
            Type = "docker",
            Revisions =
            [
                new GameTypeRevision
                {
                    ImageReference = "itzg/minecraft-server",
                    VersionTag = "latest",
                    ImageDigest = "sha256:test",
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
                    ],
                    WebHosts =
                    [
                        new GameTypeWebHost
                        {
                            Name = "Dynmap",
                            ContainerPort = 8123,
                            DisplayOrder = 0
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
        Assert.Equal("boolean", loaded.Revisions[0].SettingDefinitions[0].Metadata?.DataType);
    }

    [Fact]
    public async Task AddRevisionAsync_WhenAdvertisedPortMissing_ShouldThrow()
    {
        var gameType = await _repository.CreateAsync(new GameType
        {
            Key = "valheim-v2",
            DisplayName = "Valheim V2",
            Type = "docker"
        });

        var revision = new GameTypeRevision
        {
            ImageReference = "lloesche/valheim-server",
            VersionTag = "latest",
            Ports =
            [
                new GameTypePort { ContainerPort = 2456, Protocol = "udp", AdvertisedPort = false }
            ]
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.AddRevisionAsync(gameType.Key, revision));
    }
}
