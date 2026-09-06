using GameServer.Docker.Agent.Hubs;
using GameServer.Docker.Agent.Interfaces;
using GameServer.Docker.Agent.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace GameServer.Docker.Agent.Tests.Hubs;

public class NodeAgentHubTests
{
    private readonly Mock<ILogger<NodeAgentHub>> _mockLogger;
    private readonly Mock<IContainerService> _mockContainerService;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly NodeAgentHub _hub;

    public NodeAgentHubTests()
    {
        _mockLogger = new Mock<ILogger<NodeAgentHub>>();
        _mockContainerService = new Mock<IContainerService>();
        _mockContext = new Mock<HubCallerContext>();
        _mockContext.SetupGet(c => c.ConnectionId).Returns("conn-123");

        _hub = new NodeAgentHub(_mockLogger.Object, _mockContainerService.Object)
        {
            Context = _mockContext.Object
        };
    }

    [Fact]
    public async Task GetContainerStatsSnapshot_WhenSuccessful_ReturnsStats()
    {
        var expectedStats = new ContainerStatsResponse
        {
            ContainerId = "c-1",
            Cpu = new CpuStats { UsagePercent = 15.0 }
        };

        _mockContainerService
            .Setup(c => c.GetContainerStatsAsync("c-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        var result = await _hub.GetContainerStatsSnapshot("c-1");

        Assert.NotNull(result);
        Assert.Same(expectedStats, result);
    }

    [Fact]
    public async Task GetContainerStatsSnapshot_WhenServiceThrows_Rethrows()
    {
        _mockContainerService
            .Setup(c => c.GetContainerStatsAsync("c-1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Docker error"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _hub.GetContainerStatsSnapshot("c-1"));
    }

    [Fact]
    public async Task GetContainerLogs_WhenSuccessful_ReturnsLogsObject()
    {
        var logsResponse = new ContainerLogsResponse
        {
            ContainerId = "c-1",
            LogLines = 2,
            Logs = ["line1", "line2"]
        };

        _mockContainerService
            .Setup(c => c.GetContainerLogsAsync("c-1", 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(logsResponse);

        var result = await _hub.GetContainerLogs("c-1", 50);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetContainerLogs_WhenServiceThrows_Rethrows()
    {
        _mockContainerService
            .Setup(c => c.GetContainerLogsAsync("c-1", 100, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Log error"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _hub.GetContainerLogs("c-1", 100));
    }

    [Fact]
    public async Task StreamContainerStats_YieldsStatsFromService()
    {
        var stats1 = new ContainerStatsResponse { ContainerId = "c-1", Cpu = new CpuStats { UsagePercent = 10.0 } };
        var stats2 = new ContainerStatsResponse { ContainerId = "c-1", Cpu = new CpuStats { UsagePercent = 20.0 } };

        async IAsyncEnumerable<ContainerStatsResponse> GenerateStats()
        {
            yield return stats1;
            yield return stats2;
            await Task.CompletedTask;
        }

        _mockContainerService
            .Setup(c => c.StreamContainerStatsAsync("c-1", It.IsAny<CancellationToken>()))
            .Returns(GenerateStats());

        var items = new List<object>();
        await foreach (var item in _hub.StreamContainerStats("c-1", CancellationToken.None))
        {
            items.Add(item);
        }

        Assert.Equal(2, items.Count);
        Assert.Same(stats1, items[0]);
        Assert.Same(stats2, items[1]);
    }

    [Fact]
    public async Task StreamContainerLogs_YieldsLogLinesFromService()
    {
        async IAsyncEnumerable<string> GenerateLogs()
        {
            yield return "[2026-09-06] Server started";
            yield return "[2026-09-06] Listening on port 25565";
            await Task.CompletedTask;
        }

        _mockContainerService
            .Setup(c => c.StreamContainerLogsAsync("c-1", true, 100, true, It.IsAny<CancellationToken>()))
            .Returns(GenerateLogs());

        var lines = new List<string>();
        await foreach (var line in _hub.StreamContainerLogs("c-1", true, 100, true, CancellationToken.None))
        {
            lines.Add(line);
        }

        Assert.Equal(2, lines.Count);
        Assert.Contains("Server started", lines[0]);
        Assert.Contains("Listening on port", lines[1]);
    }

    [Fact]
    public async Task OnDisconnectedAsync_CleansUpSessionWithoutThrowing()
    {
        var exception = new Exception("Connection lost");
        await _hub.OnDisconnectedAsync(exception);
    }
}
