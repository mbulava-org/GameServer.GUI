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
            Mock.Of<IGameServerReadinessWatcherService>(),
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
            Mock.Of<IGameServerReadinessWatcherService>(),
            Mock.Of<ILogger<GameServerResourceCollectorService>>());

        await collector.TriggerImmediateCollectionAsync("srv-shutdown");

        // Stop the service
        await collector.StopAsync(CancellationToken.None);

        Assert.True(flushed, "StopAsync should have flushed the remaining buffered metrics to the repository.");
    }

    [Fact]
    public async Task TriggerImmediateCollectionAsync_WhenServerIsStopped_ShouldCacheUsageButNotBufferForDatabase()
    {
        var services = new ServiceCollection();
        var mockMonitor = new Mock<IServerResourceMonitor>();
        var mockRepo = new Mock<IGameServerResourceUtilizationRepository>();
        var readinessGate = new DatabaseReadinessGate();
        readinessGate.MarkReady();

        var stoppedUsage = new ServerResourceUsage
        {
            ServerId = "srv-stopped",
            Timestamp = DateTime.UtcNow,
            RealTimeStats = null,
            DesiredReplicas = 0,
            RunningReplicas = 0
        };

        mockMonitor
            .Setup(m => m.GetSnapshotAsync("srv-stopped", It.IsAny<CancellationToken>()))
            .ReturnsAsync(stoppedUsage);

        services.AddScoped(_ => mockMonitor.Object);
        services.AddScoped(_ => mockRepo.Object);
        var serviceProvider = services.BuildServiceProvider();

        var collector = new GameServerResourceCollectorService(
            serviceProvider,
            readinessGate,
            Mock.Of<IGameServerReadinessWatcherService>(),
            Mock.Of<ILogger<GameServerResourceCollectorService>>());

        await collector.TriggerImmediateCollectionAsync("srv-stopped");

        var cached = collector.GetCachedUsage("srv-stopped");
        Assert.NotNull(cached);
        Assert.Equal("srv-stopped", cached!.ServerId);
        Assert.Equal(0, cached.RunningReplicas);

        var batchCalled = false;
        mockRepo
            .Setup(r => r.BatchInsertAsync(It.IsAny<IEnumerable<GameServerResourceUtilizationEntity>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<GameServerResourceUtilizationEntity>, CancellationToken>((recs, _) =>
            {
                if (recs.Any())
                {
                    batchCalled = true;
                }
            })
            .Returns(Task.CompletedTask);

        await collector.FlushAsync();

        Assert.False(batchCalled, "Stopped servers without active container stats should not be saved to the database.");
    }

    [Fact]
    public async Task TriggerImmediateCollectionAsync_WhenNoRealTimeStats_ShouldNotBufferForDatabase()
    {
        var services = new ServiceCollection();
        var mockMonitor = new Mock<IServerResourceMonitor>();
        var mockRepo = new Mock<IGameServerResourceUtilizationRepository>();
        var readinessGate = new DatabaseReadinessGate();
        readinessGate.MarkReady();

        var emptyMetricsUsage = new ServerResourceUsage
        {
            ServerId = "srv-no-stats",
            Timestamp = DateTime.UtcNow,
            RealTimeStats = null,
            DesiredReplicas = 1,
            RunningReplicas = 1
        };

        mockMonitor
            .Setup(m => m.GetSnapshotAsync("srv-no-stats", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyMetricsUsage);

        services.AddScoped(_ => mockMonitor.Object);
        services.AddScoped(_ => mockRepo.Object);
        var serviceProvider = services.BuildServiceProvider();

        var collector = new GameServerResourceCollectorService(
            serviceProvider,
            readinessGate,
            Mock.Of<IGameServerReadinessWatcherService>(),
            Mock.Of<ILogger<GameServerResourceCollectorService>>());

        await collector.TriggerImmediateCollectionAsync("srv-no-stats");

        var batchCalled = false;
        mockRepo
            .Setup(r => r.BatchInsertAsync(It.IsAny<IEnumerable<GameServerResourceUtilizationEntity>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<GameServerResourceUtilizationEntity>, CancellationToken>((recs, _) =>
            {
                if (recs.Any())
                {
                    batchCalled = true;
                }
            })
            .Returns(Task.CompletedTask);

        await collector.FlushAsync();

        Assert.False(batchCalled, "Server without real-time stats should not be saved to the database.");
    }
}
