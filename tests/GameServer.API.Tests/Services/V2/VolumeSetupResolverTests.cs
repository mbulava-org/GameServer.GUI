using GameServer.API.Models.V2;
using GameServer.API.Repositories.V2;
using GameServer.API.Services.V2;
using GameServer.API.Services.V2.MountTypeHandlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GameServer.API.Tests.Services.V2;

public class VolumeSetupResolverTests
{
    private static readonly MountTypeConfig VolumeConfig = new()
    {
        Key = "volume",
        DisplayName = "Docker volume",
        Options = new Dictionary<string, string>
        {
            ["Driver"] = "local",
            ["DriverOptionsJson"] = "{\"type\":\"nfs\",\"device\":\":/exported/gameservers\",\"o\":\"addr=host.docker.internal,rw\"}",
            ["SourcePathTemplate"] = "{gameTypeKey}_{serverId}_{Source}"
        }
    };

    private static readonly MountTypeConfig BindConfig = new()
    {
        Key = "bind",
        DisplayName = "Bind mount",
        Options = new Dictionary<string, string>
        {
            ["Driver"] = "local",
            ["SourcePathTemplate"] = "/host/gameservers/{gameTypeKey}/{serverId}/{Source}"
        }
    };

    private static readonly MountTypeConfig TmpfsConfig = new()
    {
        Key = "tmpfs",
        DisplayName = "tmpfs",
        Options = new Dictionary<string, string>
        {
            ["Driver"] = "local",
            ["SourcePathTemplate"] = string.Empty
        }
    };

    private static readonly MountTypeConfig NfsConfig = new()
    {
        Key = "nfs",
        DisplayName = "NFS volume",
        Options = new Dictionary<string, string>
        {
            ["Driver"] = "local",
            ["NfsOptions"] = "addr=host.docker.internal,rw",
            ["NfsRoot"] = "/exported/path",
            ["DevicePathFormat"] = "{gameTypeKey}/{serverId}/{Source}",
            ["LocalPath"] = "/data/nfs",
            ["SourcePathTemplate"] = "{gameTypeKey}_{serverId}_{Source}"
        }
    };

    private static IMountTypeHandlerFactory CreateHandlerFactory()
    {
        var handlers = new IMountTypeHandler[]
        {
            new VolumeMountTypeHandler(NullLogger<VolumeMountTypeHandler>.Instance),
            new NfsMountTypeHandler(NullLogger<NfsMountTypeHandler>.Instance)
        };
        return new MountTypeHandlerFactory(handlers);
    }

    private static VolumeSetupResolver CreateResolver(MountTypeConfig? config = null)
    {
        var effectiveVolumeConfig = config?.Key == "volume" ? config : VolumeConfig;
        var repository = new Mock<GameServer.API.Repositories.V2.IMountTypeConfigRepository>();
        repository
            .Setup(x => x.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) =>
                string.Equals(key, "volume", StringComparison.OrdinalIgnoreCase) ? effectiveVolumeConfig :
                string.Equals(key, "bind", StringComparison.OrdinalIgnoreCase) ? (config?.Key == "bind" ? config : BindConfig) :
                string.Equals(key, "nfs", StringComparison.OrdinalIgnoreCase) ? (config?.Key == "nfs" ? config : NfsConfig) :
                string.Equals(key, "tmpfs", StringComparison.OrdinalIgnoreCase) ? (config?.Key == "tmpfs" ? config : TmpfsConfig) : (config ?? VolumeConfig));
        return new VolumeSetupResolver(repository.Object, CreateHandlerFactory(), NullLogger<VolumeSetupResolver>.Instance);
    }

    [Fact]
    public async Task ResolveForCreateAsync_ShouldResolveSnapshots()
    {
        var resolver = CreateResolver();
        var revisionVolume = new GameTypeVolume
        {
            Source = "/data/worlds",
            Usage = "worlds",
            MountType = "volume"
        };

        var resolved = await resolver.ResolveForCreateAsync("srv1", "minecraft", [revisionVolume]);

        var resolution = Assert.Single(resolved);
        var snapshot = resolution.Snapshot;
        Assert.Equal("/data/worlds", snapshot.ContainerPath);
        Assert.Equal("volume", snapshot.MountType);
        Assert.Equal("minecraft_srv1_data-worlds", snapshot.VolumeName);
    }

    [Fact]
    public async Task ResolveForUpdateAsync_ShouldReturnOnlyNewSnapshots()
    {
        var resolver = CreateResolver();
        var existing = new GameServerVolume
        {
            ContainerPath = "/data/worlds",
            Usage = "worlds"
        };
        var revisionVolumes = new List<GameTypeVolume>
        {
            new() { Source = "/data/worlds", Usage = "worlds", MountType = "volume" },
            new() { Source = "/data/config", Usage = "config", MountType = "volume" }
        };

        var resolved = await resolver.ResolveForUpdateAsync("srv1", "minecraft", revisionVolumes, [existing]);

        var resolution = Assert.Single(resolved);
        Assert.Equal("/data/config", resolution.Snapshot.ContainerPath);
    }

    [Fact]
    public async Task ResolveForCreateAsync_ShouldBakeNfsDriverOptionsFromMountType()
    {
        var resolver = CreateResolver();
        var revisionVolume = new GameTypeVolume
        {
            Source = "/data/worlds",
            Usage = "worlds",
            MountType = "nfs",
            EnsureNfsPathExists = true
        };

        var resolved = await resolver.ResolveForCreateAsync("srv1", "minecraft", [revisionVolume]);

        var resolution = Assert.Single(resolved);
        var snapshot = resolution.Snapshot;
        Assert.Equal("nfs", snapshot.MountType);

        var options = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(snapshot.DriverOptionsJson!);
        Assert.NotNull(options);
        Assert.Equal("nfs", options!["type"]);
        Assert.Equal("addr=host.docker.internal,rw", options["o"]);
        Assert.Equal(":/exported/path/minecraft/srv1/data-worlds", options["device"]);

        // Provisioning-only data lives on the transient spec, not the persisted snapshot.
        Assert.True(resolution.Provisioning.EnsureNfsPathExists);
    }

    [Fact]
    public async Task ResolveForCreateAsync_WhenOwnerVariablesProvided_ShouldResolveUidAndGid()
    {
        var resolver = CreateResolver();
        var revisionVolume = new GameTypeVolume
        {
            Source = "/data",
            Usage = "world",
            MountType = "volume",
            OwnerUidVariable = "UID",
            OwnerGidVariable = "GID"
        };

        var settings = new Dictionary<string, string?>
        {
            ["UID"] = "1001",
            ["GID"] = "1002"
        };

        var resolved = await resolver.ResolveForCreateAsync("srv1", "minecraft", [revisionVolume], settingValues: settings);

        var resolution = Assert.Single(resolved);
        Assert.Equal(1001, resolution.Provisioning.OwnerUid);
        Assert.Equal(1002, resolution.Provisioning.OwnerGid);
    }

    [Fact]
    public async Task ResolveForCreateAsync_WhenServerNameProvided_ShouldReplaceServerNameToken()
    {
        var customConfig = new MountTypeConfig
        {
            Key = "volume",
            DisplayName = "Docker volume",
            VolumeNameFormat = "{serverName}_{Usage}"
        };

        var resolver = CreateResolver(customConfig);
        var revisionVolume = new GameTypeVolume
        {
            Source = "/data",
            Usage = "world",
            MountType = "volume"
        };

        var settings = new Dictionary<string, string?>
        {
            ["Name"] = "My Cool Server"
        };

        var resolved = await resolver.ResolveForCreateAsync("srv1", "minecraft", [revisionVolume], settingValues: settings);

        var resolution = Assert.Single(resolved);
        Assert.Equal("my-cool-server_world", resolution.Snapshot.VolumeName);
    }
}
