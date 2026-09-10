using GameServer.Windows.Agent.Controllers;
using GameServer.Windows.Agent.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GameServer.Windows.Agent.Tests.Controllers;

public class HealthControllerTests
{
    private readonly Mock<IWindowsResourceMonitor> _resourceMonitorMock;
    private readonly HealthController _controller;

    public HealthControllerTests()
    {
        _resourceMonitorMock = new Mock<IWindowsResourceMonitor>();
        _controller = new HealthController(_resourceMonitorMock.Object);
    }

    [Fact]
    public void GetHealth_ReturnsOkWithExpectedPayload()
    {
        // Arrange
        var snapshot = new HostResourceSnapshot
        {
            TotalMemoryBytes = 16 * 1024 * 1024 * 1024L,
            FreeMemoryBytes = 8 * 1024 * 1024 * 1024L,
            HostCpuPercent = 12.5
        };
        _resourceMonitorMock.Setup(m => m.GetHostSnapshot()).Returns(snapshot);

        // Act
        var result = _controller.GetHealth();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        dynamic payload = okResult.Value;
        Assert.Equal("Healthy", (string)payload.status);
        Assert.Equal("GameServer.Windows.Agent", (string)payload.agent);
        Assert.Equal("Windows", (string)payload.platform);
        Assert.NotNull(payload.version);
        Assert.Same(snapshot, (HostResourceSnapshot)payload.host);
    }
}
