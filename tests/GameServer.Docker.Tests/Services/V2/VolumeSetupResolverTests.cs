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
        Driver = "local",
        DriverOptionsJson = "{\"type\":\"nfs\",\"device\":\":/exported/gameservers\",\"o\":\"addr=host.docker.internal,rw\"}",
        SourcePathTemplate = "{gameTypeKey}_{serverId}_{Source}",
        ContainerPathTemplate = "{Source}"
    };

    private static readonly MountTypeConfig BindConfig = new()
    {
        Key = "bind",
        DisplayName = "Bind mount",
        Driver = "local",
        SourcePathTemplate = "/host/gameservers/{gameTypeKey}/{serverId}/{Source}",
        ContainerPathTemplate = "{Source}"
    };

    private static readonly MountTypeConfig TmpfsConfig = new()
    {
        Key = "tmpfs",
        DisplayName = "tmpfs",
        Driver = "local",
        SourcePathTemplate = string.Empty,
        ContainerPathTemplate = "{Source}"
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
    public void ResolveForCreate_ShouldResolveVolumeSourceAndContainerPath()
    {
        var resolver = CreateResolver();
        var revisionVolume = new GameTypeVolume
        {
            Source = "/data/worlds",
            Usage = "worlds"
        };

        var result = resolver.ResolveForCreate("srv1", "minecraft", [revisionVolume]);

        Assert.Single(result);
        Assert.Equal("/minecraft_srv1_/data/worlds", result[0].Source);
        Assert.Equal("/data/worlds", result[0].ContainerPath);
        Assert.Equal("volume", result[0].MountType);
    }

    [Fact]
    public void ResolveForCreate_StandardLayout_ShouldIncludeNfsDriverOptions()
    {
        var resolver = CreateResolver();
        var revisionVolume = new GameTypeVolume
        {
            Source = "/data/worlds",
            Usage = "worlds"
        };

        var result = resolver.ResolveForCreate("srv1", "minecraft", [revisionVolume], layout: "standard");

        Assert.Single(result);
        Assert.NotNull(result[0].DriverOptionsJson);
        Assert.Contains("\"type\":\"nfs\"", result[0].DriverOptionsJson);
    }

    [Fact]
    public void ResolveForCreate_ShouldPreserveOwnershipAndPermissions()
    {
        var resolver = CreateResolver();
        var revisionVolume = new GameTypeVolume
        {
            Source = "/data/worlds",
            Usage = "worlds",
            OwnerUid = 1000,
            OwnerGid = 1000,
            Permissions = "0755"
        };

        var result = resolver.ResolveForCreate("srv1", "minecraft", [revisionVolume]);

        Assert.Equal(1000, result[0].OwnerUid);
        Assert.Equal(1000, result[0].OwnerGid);
        Assert.Equal("0755", result[0].Permissions);
    }

    [Fact]
    public void ResolveForUpdate_WithExistingSnapshot_ShouldOnlyReturnNewVolumes()
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

        var result = resolver.ResolveForUpdate("srv1", "minecraft", revisionVolumes, [existing]);

        Assert.Single(result);
        Assert.Equal("/data/config", result[0].ContainerPath);
    }

    [Fact]
    public void BuildMountConfigs_ShouldProduceAgentMountConfigs()
    {
        var resolver = CreateResolver();
        var resolved = resolver.ResolveForCreate("srv1", "minecraft",
        [
            new GameTypeVolume { Source = "/data/worlds", Usage = "worlds", ReadOnly = true }
        ]);

        var mounts = resolver.BuildMountConfigs(resolved);

        Assert.Single(mounts);
        dynamic mount = mounts[0];
        Assert.Equal("volume", (string)mount.Type);
        Assert.Equal("/data/worlds", (string)mount.Target);
        Assert.True((bool)mount.ReadOnly);
        Assert.Equal("local", (string?)mount.DriverName);
    }

    [Fact]
    public void ResolveForCreate_BindMount_ShouldNotSetDriverOptionsJson()
    {
        var resolver = CreateResolver();
        var revisionVolume = new GameTypeVolume
        {
            Source = "/data/worlds",
            Usage = "worlds",
            MountType = "bind"
        };

        var result = resolver.ResolveForCreate("srv1", "minecraft", [revisionVolume]);

        Assert.Null(result[0].DriverOptionsJson);
    }
}
