using GameServer.API.Data.V2;
using GameServer.API.Repositories.V2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using GameServerModel = GameServer.API.Models.V2.GameServer;
using GameServerSettingModel = GameServer.API.Models.V2.GameServerSetting;

namespace GameServer.API.Tests.Repositories.V2;

public class GameServerRepositoryTests : IDisposable
{
    private readonly GameServerV2DbContext _context;
    private readonly GameServerRepository _repository;
    private readonly int _revisionId;

    public GameServerRepositoryTests()
    {
        var dbPath = $":memory:_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<GameServerV2DbContext>()
            .UseSqlite($"DataSource={dbPath};Mode=Memory;Cache=Shared")
            .Options;

        _context = new GameServerV2DbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        var gameType = new GameTypeEntity
        {
            Key = "test-game",
            DisplayName = "Test Game",
            Type = "docker"
        };

        var revision = new GameTypeRevisionEntity
        {
            GameType = gameType,
            ImageReference = "repo/test",
            VersionTag = "latest",
            ImageDigest = "sha256:test",
            IsPublished = true
        };

        _context.GameTypes.Add(gameType);
        _context.GameTypeRevisions.Add(revision);
        _context.SaveChanges();

        _revisionId = revision.Id;
        _repository = new GameServerRepository(_context, Mock.Of<ILogger<GameServerRepository>>());
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public async Task CreateAsync_WhenServerHasSettings_ShouldPersistAggregate()
    {
        var server = new GameServerModel
        {
            ServerId = "server-001",
            Name = "Alpha",
            GameTypeRevisionId = _revisionId,
            ServiceName = "alpha-service",
            Status = "Created",
            Settings =
            [
                new GameServerSettingModel { SettingKey = "EULA", Value = "TRUE" }
            ]
        };

        var created = await _repository.CreateAsync(server);
        var loaded = await _repository.GetByServerIdAsync(server.ServerId);

        Assert.NotNull(loaded);
        Assert.Equal(created.Id, loaded.Id);
        Assert.Equal(_revisionId, loaded.GameTypeRevisionId);
        Assert.Single(loaded.Settings);
    }

    [Fact]
    public async Task UpdateAsync_WhenSettingsModified_ShouldReconcileInPlaceWithoutUniqueConstraintViolation()
    {
        var server = new GameServerModel
        {
            ServerId = "server-003",
            Name = "Charlie",
            GameTypeRevisionId = _revisionId,
            ServiceName = "charlie-service",
            Status = "Created",
            Settings =
            [
                new GameServerSettingModel { SettingKey = "Difficulty", Value = "Normal" },
                new GameServerSettingModel { SettingKey = "MaxPlayers", Value = "10" }
            ],
            Ports =
            [
                new GameServer.API.Models.V2.GameServerPort { ContainerPort = 25565, Protocol = "tcp", PublishedPort = 25565 }
            ]
        };

        var created = await _repository.CreateAsync(server);

        // Update with modified setting value, one removed setting, and one new setting
        var updatedServer = created with
        {
            Status = "Running",
            Settings =
            [
                new GameServerSettingModel { SettingKey = "Difficulty", Value = "Hard" },
                new GameServerSettingModel { SettingKey = "MOTD", Value = "Welcome" }
            ],
            Ports =
            [
                new GameServer.API.Models.V2.GameServerPort { ContainerPort = 25565, Protocol = "tcp", PublishedPort = 25566 }
            ]
        };

        var result = await _repository.UpdateAsync(updatedServer);
        var loaded = await _repository.GetByServerIdAsync("server-003");

        Assert.NotNull(loaded);
        Assert.Equal("Running", loaded.Status);
        Assert.Equal(2, loaded.Settings.Count);
        Assert.Equal("Hard", loaded.Settings.First(s => s.SettingKey == "Difficulty").Value);
        Assert.Equal("Welcome", loaded.Settings.First(s => s.SettingKey == "MOTD").Value);
        Assert.DoesNotContain(loaded.Settings, s => s.SettingKey == "MaxPlayers");
        Assert.Single(loaded.Ports);
        Assert.Equal(25566, loaded.Ports[0].PublishedPort);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenServerExists_ShouldUpdateStatusDirectly()
    {
        await _repository.CreateAsync(new GameServerModel
        {
            ServerId = "server-004",
            Name = "Delta",
            GameTypeRevisionId = _revisionId,
            ServiceName = "delta-service",
            Status = "Starting"
        });

        await _repository.UpdateStatusAsync("server-004", "Available");

        var loaded = await _repository.GetByServerIdAsync("server-004");
        Assert.NotNull(loaded);
        Assert.Equal("Available", loaded.Status);
    }

    [Fact]
    public async Task DeleteAsync_WhenSoftDeleteRequested_ShouldMarkEntityDeleted()
    {
        await _repository.CreateAsync(new GameServerModel
        {
            ServerId = "server-002",
            Name = "Bravo",
            GameTypeRevisionId = _revisionId,
            ServiceName = "bravo-service",
            Status = "Created"
        });

        await _repository.DeleteAsync("server-002", softDelete: true);

        var deleted = await _repository.GetByServerIdAsync("server-002");

        Assert.NotNull(deleted);
        Assert.True(deleted!.IsDeleted);
    }
}
