using GameServer.Windows.Agent.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace GameServer.Windows.Agent.Tests.Services;

public class WindowsResourceMonitorTests
{
    [Fact]
    public void GetHostSnapshot_ReturnsNonNullResourceSnapshot()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<WindowsResourceMonitor>>();
        var monitor = new WindowsResourceMonitor(loggerMock.Object);

        // Act
        var snapshot = monitor.GetHostSnapshot();

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.TotalMemoryBytes > 0);
        Assert.True(snapshot.FreeMemoryBytes >= 0);
        Assert.NotNull(snapshot.Drives);
        Assert.True(snapshot.HostCpuPercent >= 0);
    }
}
