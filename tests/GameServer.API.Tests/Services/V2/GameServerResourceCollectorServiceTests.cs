using GameServer.API.Data.V2;
using GameServer.API.Interfaces;
using GameServer.API.Models;
using GameServer.API.Repositories.V2;
using GameServer.API.Services;
using GameServer.API.Services.V2;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace GameServer.API.Tests.Services.V2;

public class GameServerResourceCollectorServiceTests
{
    [Fact]
    public async Task TriggerImmediateCollectionAsync_WhenServerFound_ShouldCacheUsageAndBufferForDatabase()
    {
        var services = new ServiceCollection();
        var mockMonitor = new Mock<IServerResourceMonitor>();
        var mockRepo = new Mock<IGameServerResourceUtilizationRepository>();
        var readinessGate = new DatabaseReadinessGate();
        readinessGate.MarkReady();

        var testUsage = new ServerResourceUsage
        {
            ServerId = "srv-101",
            Timestamp = DateTime.UtcNow,
            RealTimeStats = new ContainerStats
            {
                CpuUsagePercent = 45.5,
                MemoryUsageBytes = 1024 * 1024 * 512,
                MemoryLimitBytes = 1024 * 1024 * 1024,
                MemoryUsagePercent = 50.0,
                NetworkRxBytes = 1000,
                NetworkTxBytes = 2000,
                BlockReadBytes = 3000,
                BlockWriteBytes = 4000
            },
            DesiredReplicas = 1,
            RunningReplicas = 1
        };

        mockMonitor
            .Setup(m => m.GetSnapshotAsync("srv-101", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testUsage);

        services.AddScoped(_ => mockMonitor.Object);
        services.AddScoped(_ => mockRepo.Object);
        var serviceProvider = services.BuildServiceProvider();

        var collector = new GameServerResourceCollectorService(
            serviceProvider,
            readinessGate,
            Mock.Of<ILogger<GameServerResourceCollectorService>>());

        await collector.TriggerImmediateCollectionAsync("srv-101");

        var cached = collector.GetCachedUsage("srv-101");
        Assert.NotNull(cached);
        Assert.Equal("srv-101", cached!.ServerId);
        Assert.Equal(45.5, cached.CpuUsagePercent);

        // Now trigger flush
        List<GameServerResourceUtilizationEntity>? savedRecords = null;
        mockRepo
            .Setup(r => r.BatchInsertAsync(It.IsAny<IEnumerable<GameServerResourceUtilizationEntity>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<GameServerResourceUtilizationEntity>, CancellationToken>((recs, _) =>
            {
                savedRecords = recs.ToList();
            })
            .Returns(Task.CompletedTask);

        await collector.FlushAsync();

        Assert.NotNull(savedRecords);
        Assert.Single(savedRecords!);
        Assert.Equal("srv-101", savedRecords![0].ServerId);
        Assert.Equal(45.5, savedRecords![0].CpuUsagePercent);
    }

    [Fact]
    public async Task StopAsync_WhenBufferContainsRecords_ShouldFlushToDatabaseBeforeCompleting()
    {
        var services = new ServiceCollection();
        var mockMonitor = new Mock<IServerResourceMonitor>();
        var mockRepo = new Mock<IGameServerResourceUtilizationRepository>();
        var readinessGate = new DatabaseReadinessGate();
        readinessGate.MarkReady();

        mockMonitor
            .Setup(m => m.GetSnapshotAsync("srv-shutdown", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServerResourceUsage
            {
                ServerId = "srv-shutdown",
                Timestamp = DateTime.UtcNow,
                RealTimeStats = new ContainerStats { CpuUsagePercent = 20.0 },
                DesiredReplicas = 1,
                RunningReplicas = 1
            });

        var flushed = false;
        mockRepo
            .Setup(r => r.BatchInsertAsync(It.IsAny<IEnumerable<GameServerResourceUtilizationEntity>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<GameServerResourceUtilizationEntity>, CancellationToken>((recs, _) =>
            {
                if (recs.Any(r => r.ServerId == "srv-shutdown"))
                {
                    flushed = true;
                }
            })
            .Returns(Task.CompletedTask);

        services.AddScoped(_ => mockMonitor.Object);
        services.AddScoped(_ => mockRepo.Object);
        var serviceProvider = services.BuildServiceProvider();

        var collector = new GameServerResourceCollectorService(
            serviceProvider,
            readinessGate,
            Mock.Of<ILogger<GameServerResourceCollectorService>>());

        await collector.TriggerImmediateCollectionAsync("srv-shutdown");

        // Stop the service
        await collector.StopAsync(CancellationToken.None);

        Assert.True(flushed, "StopAsync should have flushed the remaining buffered metrics to the repository.");
    }
}
