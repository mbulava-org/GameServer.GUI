using GameServer.Windows.Agent.Controllers;
using GameServer.Windows.Agent.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GameServer.Windows.Agent.Tests.Controllers;

public class PortsControllerTests
{
    private readonly Mock<IWindowsPortService> _portServiceMock;
    private readonly PortsController _controller;

    public PortsControllerTests()
    {
        _portServiceMock = new Mock<IWindowsPortService>();
        _controller = new PortsController(_portServiceMock.Object);
    }

    [Fact]
    public void CheckPort_CallsService_AndReturnsAvailability()
    {
        // Arrange
        _portServiceMock.Setup(s => s.IsPortAvailable(7777, "udp")).Returns(true);

        // Act
        var result = _controller.CheckPort(7777, "udp");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(true, okResult.Value);
        _portServiceMock.Verify(s => s.IsPortAvailable(7777, "udp"), Times.Once);
    }

    [Fact]
    public void CheckPort_WhenPortInUse_ReturnsFalse()
    {
        // Arrange
        _portServiceMock.Setup(s => s.IsPortAvailable(27015, "udp")).Returns(false);

        // Act
        var result = _controller.CheckPort(27015, "udp");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(false, okResult.Value);
    }

    [Fact]
    public void CheckBatch_ReturnsBatchResults()
    {
        // Arrange
        var requests = new List<PortsController.PortCheckRequest>
        {
            new() { Port = 7777, Protocol = "udp" },
            new() { Port = 7778, Protocol = "udp" },
            new() { Port = 27015, Protocol = "udp" }
        };

        var expectedUsage = new List<HostPortUsage>
        {
            new() { Port = 7777, Protocol = "udp", InUse = false },
            new() { Port = 7778, Protocol = "udp", InUse = false },
            new() { Port = 27015, Protocol = "udp", InUse = true }
        };

        _portServiceMock.Setup(s => s.CheckPortsAvailability(It.IsAny<IEnumerable<(int Port, string Protocol)>>()))
            .Returns(expectedUsage);

        // Act
        var result = _controller.CheckBatch(requests);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<HostPortUsage>>(okResult.Value);
        Assert.Equal(3, list.Count);
        Assert.True(list[2].InUse);
    }
}
