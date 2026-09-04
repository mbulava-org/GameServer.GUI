using System.Threading.Channels;
using GameServer.API.Interfaces;
using GameServer.API.Models;
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

    [Fact]
    public async Task SubscribeAsync_WhenLogsPreloaded_StreamsHistoricalFrames()
    {
        var discoveryMock = new Mock<INodeAgentDiscovery>();
        discoveryMock
            .Setup(d => d.GetAgentForContainerAsync("c-123"))
            .ReturnsAsync(new NodeAgentEndpoint { InternalUrl = "http://127.0.0.1:9999", NodeId = "node-1" });
        discoveryMock
            .Setup(d => d.GetContainerLogsAsync("c-123", It.IsAny<int>()))
            .ReturnsAsync(new List<string> { "Line 1: Server initialized", "Line 2: Server listening" });

        var scopeMock = new Mock<Microsoft.Extensions.DependencyInjection.IServiceScope>();
        var scopeServiceProvider = new Mock<IServiceProvider>();
        scopeServiceProvider
            .Setup(sp => sp.GetService(typeof(INodeAgentDiscovery)))
            .Returns(discoveryMock.Object);
        scopeMock.Setup(s => s.ServiceProvider).Returns(scopeServiceProvider.Object);

        var scopeFactoryMock = new Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        var rootServiceProvider = new Mock<IServiceProvider>();
        rootServiceProvider
            .Setup(sp => sp.GetService(typeof(Microsoft.Extensions.DependencyInjection.IServiceScopeFactory)))
            .Returns(scopeFactoryMock.Object);

        var aggregator = new ContainerAttachAggregator(rootServiceProvider.Object, NullLogger<ContainerAttachAggregator>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var receivedFrames = new List<AttachStreamFrame>();

        try
        {
            await foreach (var frame in aggregator.SubscribeAsync("conn-test-1", "c-123", cts.Token))
            {
                receivedFrames.Add(frame);
                if (receivedFrames.Count >= 2)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected once cancelled or timed out
        }

        Assert.NotEmpty(receivedFrames);
        Assert.Contains(receivedFrames, f => f.Kind == AttachFrameKind.Output && f.Payload.Contains("Line 1: Server initialized"));
        Assert.Contains(receivedFrames, f => f.Kind == AttachFrameKind.Output && f.Payload.Contains("Line 2: Server listening"));
    }
}
