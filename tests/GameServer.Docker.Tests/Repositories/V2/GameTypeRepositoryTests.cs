using GameServer.Docker.Data.V2;
using GameServer.Docker.Models.V2;
using GameServer.Docker.Repositories.V2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace GameServer.Docker.Tests.Repositories.V2;

public class GameTypeRepositoryTests : IDisposable
{
    private readonly SqliteGameServerV2DbContext _context;
    private readonly GameTypeRepository _repository;

    public GameTypeRepositoryTests()
    {
        var dbPath = $":memory:_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<SqliteGameServerV2DbContext>()
            .UseSqlite($"DataSource={dbPath};Mode=Memory;Cache=Shared")
            .Options;

        _context = new SqliteGameServerV2DbContext(options);
        _context.Database.OpenConnection();
        _context.Database.Migrate();

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
        var options = new DbContextOptionsBuilder<SqliteGameServerV2DbContext>()
            .UseSqlite($"DataSource={dbPath};Mode=Memory;Cache=Shared")
            .Options;

        await using var context = new SqliteGameServerV2DbContext(options);
        await context.Database.OpenConnectionAsync();

        var repository = new GameTypeRepository(context, Mock.Of<ILogger<GameTypeRepository>>());

        await repository.InitializeDatabaseAsync();

        var count = await context.GameTypes.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task InitializeDatabaseAsync_ShouldApplyAllMigrationsAndSeedMountTypes()
    {
        var dbPath = $":memory:_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<SqliteGameServerV2DbContext>()
            .UseSqlite($"DataSource={dbPath};Mode=Memory;Cache=Shared")
            .Options;

        await using var context = new SqliteGameServerV2DbContext(options);
        await context.Database.OpenConnectionAsync();

        var repository = new GameTypeRepository(context, Mock.Of<ILogger<GameTypeRepository>>());

        await repository.InitializeDatabaseAsync();
        // Running twice must be a no-op once all migrations are recorded.
        await repository.InitializeDatabaseAsync();

        var appliedMigrations = (await context.Database.GetAppliedMigrationsAsync()).ToList();
        Assert.Equal(context.Database.GetMigrations(), appliedMigrations);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());

        // Mount type defaults come from the model's HasData seed, applied by the migrations.
        Assert.True(await context.MountTypeConfigs.AnyAsync(x => x.Key == "volume"));
        Assert.True(await context.MountTypeConfigs.AnyAsync(x => x.Key == "nfs"));
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

    [Fact]
    public async Task UpdateRevisionAsync_ShouldPreserveAllVolumeFields()
    {
        var gameType = await _repository.CreateAsync(new GameType
        {
            Key = "volume-test",
            DisplayName = "Volume Test",
            Type = "docker",
            Revisions =
            [
                new GameTypeRevision
                {
                    ImageReference = "test/image",
                    VersionTag = "latest",
                    Volumes =
                    [
                        new GameTypeVolume
                        {
                            Source = "data",
                            Description = "Test volume",
                            DisplayOrder = 2,
                            Usage = "saves",
                            MountType = "nfs",
                            ReadOnly = true,
                            OwnerUid = 1000,
                            OwnerGid = 1001,
                            OwnerUidVariable = "UID",
                            OwnerGidVariable = "GID",
                            Permissions = "0770",
                            EnsureNfsPathExists = true,
                            Required = false
                        }
                    ]
                }
            ]
        });

        var loaded = await _repository.GetByKeyAsync(gameType.Key);
        Assert.NotNull(loaded);

        var revision = loaded!.Revisions.Single();
        revision.Volumes[0] = revision.Volumes[0] with
        {
            Description = "Updated test volume",
            DisplayOrder = 4,
            Usage = "logs",
            MountType = "volume",
            ReadOnly = false,
            OwnerUid = 2000,
            OwnerGid = 2001,
            OwnerUidVariable = "NEW_UID",
            OwnerGidVariable = "NEW_GID",
            Permissions = "0755",
            EnsureNfsPathExists = false,
            Required = true
        };

        await _repository.UpdateRevisionAsync(gameType.Key, revision);

        var reloaded = await _repository.GetByKeyAsync(gameType.Key);
        var volume = reloaded!.Revisions.Single().Volumes.Single();

        Assert.Equal("Updated test volume", volume.Description);
        Assert.Equal(4, volume.DisplayOrder);
        Assert.Equal("logs", volume.Usage);
        Assert.Equal("volume", volume.MountType);
        Assert.False(volume.ReadOnly);
        Assert.Equal(2000, volume.OwnerUid);
        Assert.Equal(2001, volume.OwnerGid);
        Assert.Equal("NEW_UID", volume.OwnerUidVariable);
        Assert.Equal("NEW_GID", volume.OwnerGidVariable);
        Assert.Equal("0755", volume.Permissions);
        Assert.False(volume.EnsureNfsPathExists);
        Assert.True(volume.Required);
    }
}
