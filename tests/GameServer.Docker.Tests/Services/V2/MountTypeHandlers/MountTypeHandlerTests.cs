using GameServer.Docker.Models.V2;
using GameServer.Docker.Services.V2.MountTypeHandlers;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameServer.Docker.Tests.Services.V2.MountTypeHandlers;

public class VolumeMountTypeHandlerTests
{
    private static VolumeMountTypeHandler CreateHandler()
        => new(NullLogger<VolumeMountTypeHandler>.Instance);

    private static VolumeProvisioningSpec CreateSpec(MountTypeConfig config, string volumeName = "minecraft_srv1_data")
        => new()
        {
            MountType = "volume",
            VolumeName = volumeName,
            ContainerPath = "/data",
            SourceToken = "data",
            ServerId = "srv1",
            GameTypeKey = "minecraft",
            Config = config
        };

    [Fact]
    public async Task PrepareAsync_ShouldBeNoOp()
    {
        var handler = CreateHandler();
        var spec = CreateSpec(new MountTypeConfig { Key = "volume", DisplayName = "volume" });

        await handler.PrepareAsync(spec);
    }

    [Fact]
    public void BuildDriverOptions_ShouldReturnNull_WhenNoDriverOptionsConfigured()
    {
        var handler = CreateHandler();
        var spec = CreateSpec(new MountTypeConfig { Key = "volume", DisplayName = "volume" });

        Assert.Null(handler.BuildDriverOptions(spec));
    }

    [Fact]
    public void BuildDriverOptions_ShouldSubstituteTokens()
    {
        var handler = CreateHandler();
        var config = new MountTypeConfig
        {
            Key = "volume",
            DisplayName = "volume",
            Options = new Dictionary<string, string>
            {
                ["DriverOptionsJson"] = "{\"type\":\"nfs\",\"device\":\":/exported/{Target}\"}"
            }
        };
        var spec = CreateSpec(config, volumeName: "minecraft_srv1_data");

        var json = handler.BuildDriverOptions(spec);

        Assert.NotNull(json);
        var options = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json!);
        Assert.Equal(":/exported/minecraft_srv1_data", options!["device"]);
    }

    [Fact]
    public void BuildMount_ShouldHonorSourceTargetAndReadOnly()
    {
        var handler = CreateHandler();
        var volume = new GameServerVolume
        {
            MountType = "volume",
            VolumeName = "minecraft_srv1_data",
            ContainerPath = "/data",
            ReadOnly = true
        };

        var mount = handler.BuildMount(volume);

        Assert.Equal("volume", mount.Type);
        Assert.Equal("minecraft_srv1_data", mount.Source);
        Assert.Equal("/data", mount.Target);
        Assert.True(mount.ReadOnly);
        Assert.Null(mount.VolumeOptions);
    }

    [Fact]
    public void BuildMount_ShouldApplyDriverOptionsWhenPresent()
    {
        var handler = CreateHandler();
        var volume = new GameServerVolume
        {
            MountType = "volume",
            VolumeName = "minecraft_srv1_data",
            ContainerPath = "/data",
            DriverOptionsJson = "{\"type\":\"nfs\",\"device\":\":/exported\"}"
        };

        var mount = handler.BuildMount(volume);

        Assert.NotNull(mount.VolumeOptions?.DriverConfig);
        Assert.Equal("local", mount.VolumeOptions!.DriverConfig!.Name);
        Assert.Equal("nfs", mount.VolumeOptions.DriverConfig.Options["type"]);
    }
}

public class NfsMountTypeHandlerTests
{
    private static NfsMountTypeHandler CreateHandler()
        => new(NullLogger<NfsMountTypeHandler>.Instance);

    private static VolumeProvisioningSpec CreateSpec(string localRoot, bool ensure)
        => new()
        {
            MountType = "nfs",
            VolumeName = "minecraft_srv1_data-worlds",
            ContainerPath = "/data/worlds",
            SourceToken = "data-worlds",
            ServerId = "srv1",
            GameTypeKey = "minecraft",
            EnsureNfsPathExists = ensure,
            Config = new MountTypeConfig
            {
                Key = "nfs",
                DisplayName = "nfs",
                Options = new Dictionary<string, string>
                {
                    ["NfsOptions"] = "addr=host.docker.internal,rw",
                    ["NfsRoot"] = "/exported/path",
                    ["DevicePathFormat"] = "{gameTypeKey}/{serverId}/{Source}",
                    ["LocalPath"] = localRoot
                }
            }
        };

    [Fact]
    public async Task PrepareAsync_ShouldCreateTargetDirectory_WhenEnsureRequested()
    {
        var localRoot = Path.Combine(Path.GetTempPath(), "nfs-handler-tests", Guid.NewGuid().ToString("N"));
        var expectedPath = $"{localRoot.Replace('\\', '/').TrimEnd('/')}/minecraft/srv1/data-worlds";
        var handler = CreateHandler();
        var spec = CreateSpec(localRoot, ensure: true);

        try
        {
            await handler.PrepareAsync(spec);

            Assert.True(Directory.Exists(expectedPath));
        }
        finally
        {
            if (Directory.Exists(localRoot))
            {
                Directory.Delete(localRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PrepareAsync_ShouldSkip_WhenEnsureNotRequested()
    {
        var localRoot = Path.Combine(Path.GetTempPath(), "nfs-handler-tests", Guid.NewGuid().ToString("N"));
        var handler = CreateHandler();
        var spec = CreateSpec(localRoot, ensure: false);

        await handler.PrepareAsync(spec);

        Assert.False(Directory.Exists(localRoot));
    }

    [Fact]
    public void BuildDriverOptions_ShouldProduceNfsDeviceFromMountType()
    {
        var handler = CreateHandler();
        var spec = CreateSpec("/data/nfs", ensure: true);

        var json = handler.BuildDriverOptions(spec);

        Assert.NotNull(json);
        var options = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json!);
        Assert.Equal("nfs", options!["type"]);
        Assert.Equal("addr=host.docker.internal,rw", options["o"]);
        Assert.Equal(":/exported/path/minecraft/srv1/data-worlds", options["device"]);
    }

    [Fact]
    public void BuildMount_ShouldProduceNfsMount()
    {
        var handler = CreateHandler();
        var volume = new GameServerVolume
        {
            MountType = "nfs",
            VolumeName = "minecraft_srv1_data-worlds",
            ContainerPath = "/data/worlds",
            ReadOnly = false,
            DriverOptionsJson = "{\"type\":\"nfs\",\"device\":\":/exported\"}"
        };

        var mount = handler.BuildMount(volume);

        Assert.Equal("nfs", mount.Type);
        Assert.Equal("/data/worlds", mount.Target);
        Assert.Equal("minecraft_srv1_data-worlds", mount.Source);
        Assert.NotNull(mount.VolumeOptions?.DriverConfig);
        Assert.Equal("nfs", mount.VolumeOptions!.DriverConfig!.Options["type"]);
    }
}
