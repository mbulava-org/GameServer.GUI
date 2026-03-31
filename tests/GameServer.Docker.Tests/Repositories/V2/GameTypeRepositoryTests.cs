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
    public async Task CreateAsync_WhenAggregateIncludesRevision_ShouldRoundTripAggregate()
    {
        var gameType = new GameType
        {
            Key = "minecraft-v2",
            DisplayName = "Minecraft V2",
            Description = "Versioned test game type",
            ImageReference = "itzg/minecraft-server",
            Revisions =
            [
                new GameTypeRevision
                {
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
        Assert.Equal("itzg/minecraft-server", loaded.ImageReference);
        Assert.Single(loaded.Revisions);
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
            ImageReference = "lloesche/valheim-server"
        });

        var revision = new GameTypeRevision
        {
            VersionTag = "latest",
            Ports =
            [
                new GameTypePort { ContainerPort = 2456, Protocol = "udp", AdvertisedPort = false }
            ]
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.AddRevisionAsync(gameType.Key, revision));
    }
}
