using GameServer.API.Models.V2;
using GameServer.API.Repositories.V2;
using GameServer.API.Services.V2;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameServer.API.Tests.Services.V2;

public class GameServerFilesServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly Mock<IGameServerRepository> _gameServerRepoMock;
    private readonly Mock<IGameTypeRepository> _gameTypeRepoMock;
    private readonly Mock<IMountTypeConfigRepository> _mountTypeConfigRepoMock;
    private readonly GameServerFilesService _service;

    public GameServerFilesServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "GameServerFilesTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        _gameServerRepoMock = new Mock<IGameServerRepository>();
        _gameTypeRepoMock = new Mock<IGameTypeRepository>();
        _mountTypeConfigRepoMock = new Mock<IMountTypeConfigRepository>();

        _service = new GameServerFilesService(
            _gameServerRepoMock.Object,
            _gameTypeRepoMock.Object,
            _mountTypeConfigRepoMock.Object,
            NullLogger<GameServerFilesService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            try { Directory.Delete(_tempRoot, recursive: true); } catch { }
        }
    }

    private void SetupServerAndVolume(string serverId, string volumePath)
    {
        var server = new Models.V2.GameServer
        {
            ServerId = serverId,
            Name = "Test Server",
            GameTypeRevisionId = 1,
            Volumes =
            [
                new GameServerVolume
                {
                    ContainerPath = volumePath,
                    MountType = "nfs",
                    VolumeName = "vol1",
                    Usage = "data"
                }
            ]
        };

        var gameType = new GameType
        {
            Key = "minecraft",
            DisplayName = "Minecraft",
            Revisions =
            [
                new GameTypeRevision
                {
                    Id = 1,
                    VersionTag = "latest"
                }
            ]
        };

        var mountConfig = new MountTypeConfig
        {
            Key = "nfs",
            DisplayName = "NFS Storage",
            Options = new Dictionary<string, string>
            {
                ["LocalPath"] = _tempRoot,
                ["DevicePathFormat"] = "{gameTypeKey}/{serverId}/{Source}"
            }
        };

        _gameServerRepoMock.Setup(r => r.GetByServerIdAsync(serverId))
            .ReturnsAsync(server);

        _gameTypeRepoMock.Setup(r => r.GetAllAsync(true))
            .ReturnsAsync(new List<GameType> { gameType });

        _mountTypeConfigRepoMock.Setup(r => r.GetByKeyAsync("nfs"))
            .ReturnsAsync(mountConfig);
    }

    [Fact]
    public async Task ListFilesAsync_ReturnsFilesAndDirectories()
    {
        const string serverId = "srv-123";
        const string volumePath = "/data";
        SetupServerAndVolume(serverId, volumePath);

        // Resolve expected path: _tempRoot / minecraft / srv-123 / data
        var volumeRoot = Path.Combine(_tempRoot, "minecraft", serverId, "data");
        Directory.CreateDirectory(volumeRoot);
        Directory.CreateDirectory(Path.Combine(volumeRoot, "worlds"));
        await File.WriteAllTextAsync(Path.Combine(volumeRoot, "server.properties"), "motd=Hello");

        var items = await _service.ListFilesAsync(serverId, volumePath);

        Assert.NotNull(items);
        Assert.Equal(2, items.Count);

        var dir = items.FirstOrDefault(i => i.IsDirectory);
        Assert.NotNull(dir);
        Assert.Equal("worlds", dir.Name);

        var file = items.FirstOrDefault(i => !i.IsDirectory);
        Assert.NotNull(file);
        Assert.Equal("server.properties", file.Name);
    }

    [Fact]
    public async Task GetFileContentTextAsync_And_SaveFileContentTextAsync_WorkCorrectly()
    {
        const string serverId = "srv-456";
        const string volumePath = "/data";
        SetupServerAndVolume(serverId, volumePath);

        await _service.SaveFileContentTextAsync(serverId, volumePath, "/config/settings.txt", "enable-pvp=true");

        var content = await _service.GetFileContentTextAsync(serverId, volumePath, "/config/settings.txt");
        Assert.Equal("enable-pvp=true", content);
    }

    [Fact]
    public async Task PathTraversal_ThrowsUnauthorizedAccessException()
    {
        const string serverId = "srv-789";
        const string volumePath = "/data";
        SetupServerAndVolume(serverId, volumePath);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await _service.ListFilesAsync(serverId, volumePath, "../../outside");
        });
    }

    [Fact]
    public async Task CreateDirectory_And_DeleteFileOrDirectory_WorkCorrectly()
    {
        const string serverId = "srv-abc";
        const string volumePath = "/data";
        SetupServerAndVolume(serverId, volumePath);

        await _service.CreateDirectoryAsync(serverId, volumePath, "/logs");
        var items = await _service.ListFilesAsync(serverId, volumePath);
        Assert.Contains(items, i => i.Name == "logs" && i.IsDirectory);

        await _service.DeleteFileOrDirectoryAsync(serverId, volumePath, "/logs", recursive: true);
        var itemsAfterDelete = await _service.ListFilesAsync(serverId, volumePath);
        Assert.DoesNotContain(itemsAfterDelete, i => i.Name == "logs");
    }

    [Fact]
    public async Task ListFilesAsync_WhenMountTypeHasNoLocalPath_ShouldUseFallbackAndNotThrow()
    {
        const string serverId = "srv-fallback";
        const string volumePath = "/config";
        
        var server = new Models.V2.GameServer
        {
            ServerId = serverId,
            Name = "Fallback Server",
            GameTypeRevisionId = 1,
            Volumes =
            [
                new GameServerVolume
                {
                    ContainerPath = volumePath,
                    MountType = "volume",
                    VolumeName = "vol_fallback",
                    Usage = "config"
                }
            ]
        };

        var gameType = new GameType
        {
            Key = "valheim",
            DisplayName = "Valheim",
            Revisions = [new GameTypeRevision { Id = 1, VersionTag = "latest" }]
        };

        var mountConfig = new MountTypeConfig
        {
            Key = "volume",
            DisplayName = "Docker volume",
            Options = new Dictionary<string, string>() // No LocalPath option
        };

        _gameServerRepoMock.Setup(r => r.GetByServerIdAsync(serverId)).ReturnsAsync(server);
        _gameTypeRepoMock.Setup(r => r.GetAllAsync(true)).ReturnsAsync(new List<GameType> { gameType });
        _mountTypeConfigRepoMock.Setup(r => r.GetByKeyAsync("volume")).ReturnsAsync(mountConfig);

        var items = await _service.ListFilesAsync(serverId, volumePath);

        Assert.NotNull(items);
    }
}
