using System.Security.Claims;
using GameServer.API.Data.V2;
using GameServer.API.Dtos.V2;
using GameServer.API.Interfaces;
using GameServer.API.Models;
using GameServer.API.Models.V2;
using GameServer.API.Repositories.V2;
using GameServer.API.Services.V2;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameServer.API.Tests.Services.V2;

public class GameServerBackupServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly GameServerV2DbContext _dbContext;
    private readonly Mock<IGameServerRepository> _serverRepoMock;
    private readonly Mock<IGameTypeRepository> _gameTypeRepoMock;
    private readonly Mock<IMountTypeConfigRepository> _mountConfigRepoMock;
    private readonly Mock<INodeAgentDiscovery> _nodeAgentDiscoveryMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IServerAuthorizationService> _authServiceMock;
    private readonly Mock<IGroupRepository> _groupRepoMock;
    private readonly GameServerBackupService _service;

    public GameServerBackupServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<GameServerV2DbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new GameServerV2DbContext(options);
        _dbContext.Database.EnsureCreated();

        _serverRepoMock = new Mock<IGameServerRepository>();
        _gameTypeRepoMock = new Mock<IGameTypeRepository>();
        _mountConfigRepoMock = new Mock<IMountTypeConfigRepository>();
        _nodeAgentDiscoveryMock = new Mock<INodeAgentDiscovery>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _authServiceMock = new Mock<IServerAuthorizationService>();
        _groupRepoMock = new Mock<IGroupRepository>();

        _service = new GameServerBackupService(
            _dbContext,
            _serverRepoMock.Object,
            _gameTypeRepoMock.Object,
            _mountConfigRepoMock.Object,
            _nodeAgentDiscoveryMock.Object,
            _httpClientFactoryMock.Object,
            _authServiceMock.Object,
            _groupRepoMock.Object,
            NullLogger<GameServerBackupService>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ExtendBackupRetentionAsync_WhenUnderMax_AddsExtensionDays()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "10"),
            new Claim(ClaimTypes.Name, "testuser")
        ], "TestAuth"));

        var initialExpiresAt = DateTime.UtcNow.AddDays(30);
        var entity = new GameServerBackupEntity
        {
            BackupId = "bkp-extend-1",
            ServerId = "srv-1",
            ServerName = "Server 1",
            VolumePath = "/data",
            FileName = "srv1_backup.zip",
            FilePath = Path.Combine(Path.GetTempPath(), "srv1_backup.zip"),
            CreatedByUserId = 10,
            CreatedByUsername = "testuser",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = initialExpiresAt,
            ExtensionDaysAdded = 0
        };
        _dbContext.Backups.Add(entity);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.ExtendBackupRetentionAsync(entity.Id, new ExtendBackupRequestDto { AdditionalDays = 30 }, user);

        // Assert
        Assert.Equal(30, result.ExtensionDaysAdded);
        Assert.False(result.CanExtend);
        Assert.True(result.ExpiresAt > initialExpiresAt);
    }

    [Fact]
    public async Task UpdateSharingAsync_WhenOwner_UpdatesSharingFlag()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "10"),
            new Claim(ClaimTypes.Name, "testuser")
        ], "TestAuth"));

        var entity = new GameServerBackupEntity
        {
            BackupId = "bkp-share-1",
            ServerId = "srv-1",
            ServerName = "Server 1",
            VolumePath = "/data",
            FileName = "srv1_backup.zip",
            FilePath = Path.Combine(Path.GetTempPath(), "srv1_backup.zip"),
            CreatedByUserId = 10,
            CreatedByUsername = "testuser",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            IsSharedWithGroups = false
        };
        _dbContext.Backups.Add(entity);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.UpdateSharingAsync(entity.Id, new UpdateBackupSharingRequestDto { IsSharedWithGroups = true }, user);

        // Assert
        Assert.True(result.IsSharedWithGroups);
        var saved = await _dbContext.Backups.FirstOrDefaultAsync(b => b.Id == entity.Id);
        Assert.NotNull(saved);
        Assert.True(saved.IsSharedWithGroups);
    }

    [Fact]
    public async Task GetBackupsAsync_FiltersByOwnershipAndGroupSharing()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "10"),
            new Claim(ClaimTypes.Name, "testuser")
        ], "TestAuth"));

        _groupRepoMock.Setup(g => g.GetUserGroupIdsAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([100]); // Group 100
        _groupRepoMock.Setup(g => g.GetUserGroupIdsAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync([100]); // Also member of group 100
        _groupRepoMock.Setup(g => g.GetUserGroupIdsAsync(30, It.IsAny<CancellationToken>()))
            .ReturnsAsync([200]); // Unrelated group 200

        _dbContext.Backups.AddRange(
            new GameServerBackupEntity
            {
                BackupId = "bkp-own",
                ServerId = "srv-1",
                ServerName = "Server 1",
                VolumePath = "/data",
                FileName = "own.zip",
                FilePath = "/tmp/own.zip",
                CreatedByUserId = 10,
                CreatedByUsername = "testuser",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                IsSharedWithGroups = false
            },
            new GameServerBackupEntity
            {
                BackupId = "bkp-shared-group",
                ServerId = "srv-2",
                ServerName = "Server 2",
                VolumePath = "/data",
                FileName = "shared.zip",
                FilePath = "/tmp/shared.zip",
                CreatedByUserId = 20,
                CreatedByUsername = "othergroupmember",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                IsSharedWithGroups = true // Shared with groups
            },
            new GameServerBackupEntity
            {
                BackupId = "bkp-private-other",
                ServerId = "srv-3",
                ServerName = "Server 3",
                VolumePath = "/data",
                FileName = "private.zip",
                FilePath = "/tmp/private.zip",
                CreatedByUserId = 30, // Unrelated user
                CreatedByUsername = "stranger",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                IsSharedWithGroups = false
            }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var list = await _service.GetBackupsAsync(user);

        // Assert
        Assert.Equal(2, list.Count);
        Assert.Contains(list, b => b.BackupId == "bkp-own" && b.IsOwner);
        Assert.Contains(list, b => b.BackupId == "bkp-shared-group" && !b.IsOwner);
        Assert.DoesNotContain(list, b => b.BackupId == "bkp-private-other");
    }
}
