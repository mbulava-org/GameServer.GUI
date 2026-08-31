using GameServer.API.Data.V2;
using GameServer.API.Repositories.V2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace GameServer.API.Tests.Repositories.V2;

public class GameServerResourceUtilizationRepositoryTests : IDisposable
{
    private readonly GameServerV2DbContext _context;
    private readonly GameServerResourceUtilizationRepository _repository;

    public GameServerResourceUtilizationRepositoryTests()
    {
        var dbPath = $":memory:_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<GameServerV2DbContext>()
            .UseSqlite($"DataSource={dbPath};Mode=Memory;Cache=Shared")
            .Options;

        _context = new GameServerV2DbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _repository = new GameServerResourceUtilizationRepository(
            _context,
            Mock.Of<ILogger<GameServerResourceUtilizationRepository>>());
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public async Task BatchInsertAsync_WhenGivenMultipleRecords_ShouldPersistAll()
    {
        var now = DateTime.UtcNow;
        var records = new List<GameServerResourceUtilizationEntity>
        {
            new()
            {
                ServerId = "srv-1",
                Timestamp = now.AddMinutes(-2),
                CpuUsagePercent = 12.5,
                MemoryUsageBytes = 1024 * 1024 * 500,
                DesiredReplicas = 1,
                RunningReplicas = 1,
                ContainerId = "c-1"
            },
            new()
            {
                ServerId = "srv-1",
                Timestamp = now.AddMinutes(-1),
                CpuUsagePercent = 25.0,
                MemoryUsageBytes = 1024 * 1024 * 600,
                DesiredReplicas = 1,
                RunningReplicas = 1,
                ContainerId = "c-1"
            },
            new()
            {
                ServerId = "srv-2",
                Timestamp = now,
                CpuUsagePercent = 5.0,
                MemoryUsageBytes = 1024 * 1024 * 200,
                DesiredReplicas = 1,
                RunningReplicas = 1,
                ContainerId = "c-2"
            }
        };

        await _repository.BatchInsertAsync(records);

        var srv1History = await _repository.GetHistoryAsync("srv-1");
        var srv2History = await _repository.GetHistoryAsync("srv-2");

        Assert.Equal(2, srv1History.Count);
        Assert.Single(srv2History);
    }

    [Fact]
    public async Task GetHistoryAsync_WithDateFilterAndLimit_ShouldFilterCorrectly()
    {
        var baseTime = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);
        var records = Enumerable.Range(0, 10).Select(i => new GameServerResourceUtilizationEntity
        {
            ServerId = "srv-filtered",
            Timestamp = baseTime.AddMinutes(i),
            CpuUsagePercent = i * 10.0,
            DesiredReplicas = 1,
            RunningReplicas = 1
        }).ToList();

        await _repository.BatchInsertAsync(records);

        var filtered = await _repository.GetHistoryAsync(
            "srv-filtered",
            fromUtc: baseTime.AddMinutes(3),
            toUtc: baseTime.AddMinutes(7),
            limit: 3);

        Assert.Equal(3, filtered.Count);
        // Ordered descending by timestamp
        Assert.Equal(baseTime.AddMinutes(7), filtered[0].Timestamp);
        Assert.Equal(baseTime.AddMinutes(6), filtered[1].Timestamp);
        Assert.Equal(baseTime.AddMinutes(5), filtered[2].Timestamp);
    }

    [Fact]
    public async Task GetLatestAsync_ShouldReturnMostRecentRecord()
    {
        var now = DateTime.UtcNow;
        var records = new List<GameServerResourceUtilizationEntity>
        {
            new()
            {
                ServerId = "srv-latest",
                Timestamp = now.AddMinutes(-5),
                CpuUsagePercent = 10.0,
                DesiredReplicas = 1,
                RunningReplicas = 1
            },
            new()
            {
                ServerId = "srv-latest",
                Timestamp = now.AddMinutes(-1),
                CpuUsagePercent = 88.8,
                DesiredReplicas = 1,
                RunningReplicas = 1
            }
        };

        await _repository.BatchInsertAsync(records);

        var latest = await _repository.GetLatestAsync("srv-latest");

        Assert.NotNull(latest);
        Assert.Equal(88.8, latest!.CpuUsagePercent);
    }

    [Fact]
    public async Task GetLatestAsync_WhenNoRecordsExist_ShouldReturnNull()
    {
        var latest = await _repository.GetLatestAsync("non-existent");
        Assert.Null(latest);
    }
}
