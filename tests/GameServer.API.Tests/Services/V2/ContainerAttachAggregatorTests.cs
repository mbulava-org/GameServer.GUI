using System.Threading.Channels;
using GameServer.API.Interfaces;
using GameServer.API.Services.V2;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GameServer.API.Tests.Services.V2;

public class ContainerAttachAggregatorTests
{
    [Fact]
    public async Task SendInputAsync_WhenNoActiveSource_ShouldReturnFalse()
    {
        var serviceProvider = new Mock<IServiceProvider>();
        var aggregator = new ContainerAttachAggregator(serviceProvider.Object, NullLogger<ContainerAttachAggregator>.Instance);

        var result = await aggregator.SendInputAsync("conn-1", "container-123", "help\n");

        Assert.False(result);
    }

    [Fact]
    public async Task UnsubscribeAsync_WhenNoActiveSource_ShouldNotThrow()
    {
        var serviceProvider = new Mock<IServiceProvider>();
        var aggregator = new ContainerAttachAggregator(serviceProvider.Object, NullLogger<ContainerAttachAggregator>.Instance);

        await aggregator.UnsubscribeAsync("conn-1", "container-123");
    }
}
