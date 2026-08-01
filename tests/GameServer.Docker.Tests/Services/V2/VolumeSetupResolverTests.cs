using GameServer.Docker.Models.V2;
using GameServer.Docker.Repositories.V2;
using GameServer.Docker.Services.V2;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GameServer.Docker.Tests.Services.V2;

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

    private static VolumeSetupResolver CreateResolver(MountTypeConfig? config = null)
    {
        var effectiveConfig = config ?? VolumeConfig;
        var repository = new Mock<GameServer.Docker.Repositories.V2.IMountTypeConfigRepository>();
        repository
            .Setup(x => x.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) =>
                string.Equals(key, "volume", StringComparison.OrdinalIgnoreCase) ? VolumeConfig :
                string.Equals(key, "bind", StringComparison.OrdinalIgnoreCase) ? BindConfig :
                string.Equals(key, "tmpfs", StringComparison.OrdinalIgnoreCase) ? TmpfsConfig : effectiveConfig);
        return new VolumeSetupResolver(repository.Object, NullLogger<VolumeSetupResolver>.Instance);
    }

    [Fact]
    public void ResolveForCreate_ShouldThrowNotImplementedException()
    {
        var resolver = CreateResolver();
        var revisionVolume = new GameTypeVolume
        {
            Source = "/data/worlds",
            Usage = "worlds"
        };

        Assert.Throws<NotImplementedException>(
            () => resolver.ResolveForCreate("srv1", "minecraft", [revisionVolume]));
    }

    [Fact]
    public void ResolveForUpdate_ShouldThrowNotImplementedException()
    {
        var resolver = CreateResolver();
        var existing = new GameServerVolume
        {
            ContainerPath = "/data/worlds",
            Source = "/old/worlds",
            Usage = "worlds"
        };
        var revisionVolumes = new List<GameTypeVolume>
        {
            new() { Source = "/data/worlds", Usage = "worlds" },
            new() { Source = "/data/config", Usage = "config" }
        };

        Assert.Throws<NotImplementedException>(
            () => resolver.ResolveForUpdate("srv1", "minecraft", revisionVolumes, [existing]));
    }

    [Fact]
    public void BuildMountConfigs_ShouldProduceAgentMountConfigs()
    {
        var resolver = CreateResolver();
        var resolved = new List<GameServerVolume>
        {
            new()
            {
                ContainerPath = "/data/worlds",
                Source = "/minecraft_srv1_/data/worlds",
                Usage = "worlds",
                MountType = "volume",
                ReadOnly = true,
                Driver = "local"
            }
        };

        var mounts = resolver.BuildMountConfigs(resolved);

        Assert.Single(mounts);
        dynamic mount = mounts[0];
        Assert.Equal("volume", (string)mount.Type);
        Assert.Equal("/data/worlds", (string)mount.Target);
        Assert.True((bool)mount.ReadOnly);
        Assert.Equal("local", (string?)mount.DriverName);
    }
}
