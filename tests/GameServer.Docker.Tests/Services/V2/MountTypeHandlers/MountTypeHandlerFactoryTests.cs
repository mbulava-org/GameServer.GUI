using global::Docker.DotNet.Models;
using GameServer.Docker.Models.V2;
using GameServer.Docker.Services.V2.MountTypeHandlers;

namespace GameServer.Docker.Tests.Services.V2.MountTypeHandlers;

public class MountTypeHandlerFactoryTests
{
    private sealed class FakeHandler(string key) : IMountTypeHandler
    {
        public string MountTypeKey => key;

        public string? BuildDriverOptions(VolumeProvisioningSpec spec) => null;

        public Task PrepareAsync(VolumeProvisioningSpec spec, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Mount BuildMount(GameServerVolume volume)
            => new() { Type = key };
    }

    [Fact]
    public void GetHandler_ShouldResolveRegisteredHandlerCaseInsensitively()
    {
        var factory = new MountTypeHandlerFactory([new FakeHandler("volume"), new FakeHandler("nfs")]);

        Assert.Equal("nfs", factory.GetHandler("NFS").MountTypeKey);
        Assert.Equal("volume", factory.GetHandler("volume").MountTypeKey);
    }

    [Fact]
    public void GetHandler_ShouldThrowForUnknownMountType()
    {
        var factory = new MountTypeHandlerFactory([new FakeHandler("volume")]);

        Assert.Throws<InvalidOperationException>(() => factory.GetHandler("tmpfs"));
    }

    [Fact]
    public void Constructor_ShouldThrowForDuplicateKeys()
    {
        Assert.Throws<InvalidOperationException>(
            () => new MountTypeHandlerFactory([new FakeHandler("volume"), new FakeHandler("volume")]));
    }
}
