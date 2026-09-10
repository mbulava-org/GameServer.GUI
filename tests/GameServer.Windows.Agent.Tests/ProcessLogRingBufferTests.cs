using GameServer.Windows.Agent.Services;

namespace GameServer.Windows.Agent.Tests;

public class ProcessLogRingBufferTests
{
    [Fact]
    public void Append_WithinCapacity_StoresAllLines()
    {
        // Arrange
        var buffer = new ProcessLogRingBuffer(capacity: 100);

        // Act
        for (int i = 1; i <= 50; i++)
        {
            buffer.Append($"Log line {i}");
        }

        var tail = buffer.GetTail(100);

        // Assert
        Assert.Equal(50, tail.Count);
        Assert.Equal("Log line 1", tail.First());
        Assert.Equal("Log line 50", tail.Last());
    }

    [Fact]
    public void Append_ExceedingCapacity_EvictsOldestLines()
    {
        // Arrange
        var buffer = new ProcessLogRingBuffer(capacity: 100);

        // Act
        for (int i = 1; i <= 150; i++)
        {
            buffer.Append($"Log line {i}");
        }

        var tail = buffer.GetTail(100);

        // Assert
        Assert.Equal(100, tail.Count);
        Assert.Equal("Log line 51", tail.First());
        Assert.Equal("Log line 150", tail.Last());
    }

    [Fact]
    public void GetTail_RequestingFewerLines_ReturnsExactCount()
    {
        // Arrange
        var buffer = new ProcessLogRingBuffer(capacity: 100);
        for (int i = 1; i <= 50; i++)
        {
            buffer.Append($"Log line {i}");
        }

        // Act
        var tail = buffer.GetTail(10);

        // Assert
        Assert.Equal(10, tail.Count);
        Assert.Equal("Log line 41", tail.First());
        Assert.Equal("Log line 50", tail.Last());
    }

    [Fact]
    public async Task StreamLogsAsync_ReceivesHistoricalAndLiveLines()
    {
        // Arrange
        var buffer = new ProcessLogRingBuffer(capacity: 100);
        buffer.Append("Historical line 1");
        buffer.Append("Historical line 2");

        var receivedLines = new List<string>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken,
            new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token);

        // Act
        var streamTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var line in buffer.StreamLogsAsync(includeHistory: true, historyTailLines: 10, cts.Token))
                {
                    receivedLines.Add(line);
                    if (receivedLines.Count >= 3)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on timeout
            }
        }, cts.Token);

        await Task.Delay(50, cts.Token);
        buffer.Append("Live line 3");

        await streamTask;

        // Assert
        Assert.Equal(3, receivedLines.Count);
        Assert.Equal("Historical line 1", receivedLines[0]);
        Assert.Equal("Historical line 2", receivedLines[1]);
        Assert.Equal("Live line 3", receivedLines[2]);
    }
}
