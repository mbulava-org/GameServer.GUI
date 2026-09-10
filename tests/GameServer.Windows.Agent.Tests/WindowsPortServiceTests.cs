using GameServer.Windows.Agent.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameServer.Windows.Agent.Tests;

public class WindowsPortServiceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(70000)]
    public void IsPortAvailable_InvalidPort_ReturnsFalse(int port)
    {
        // Arrange
        var service = new WindowsPortService(NullLogger<WindowsPortService>.Instance);

        // Act
        var result = service.IsPortAvailable(port, "tcp");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CheckPortsAvailability_ChecksBatch()
    {
        // Arrange
        var service = new WindowsPortService(NullLogger<WindowsPortService>.Instance);
        var requests = new List<(int Port, string Protocol)>
        {
            (0, "tcp"),
            (70000, "udp")
        };

        // Act
        var results = service.CheckPortsAvailability(requests);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal(0, results[0].Port);
        Assert.Equal("tcp", results[0].Protocol);
    }
}
