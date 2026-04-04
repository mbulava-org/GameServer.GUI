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
    private GameServerV2DbContext? _context;
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

        var optionsBuilder = new DbContextOptionsBuilder<GameServerV2DbContext>();
        GameServerV2DbContextFactory.ConfigureProvider(optionsBuilder, "mysql", _container.GetConnectionString());

        _context = new GameServerV2DbContext(optionsBuilder.Options);
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

        var canConnect = await _context.Database.CanConnectAsync();
        var count = await _context.GameTypes.CountAsync();

        Assert.True(canConnect);
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
}
