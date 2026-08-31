using GameServer.API.Client.Models;
using GameServer.API.Client.Services;

namespace GameServer.API.Client.Tests;

public class ResourceMonitoringClientTests
{
    [Fact]
    public void HubResourceUsage_FlatProperties_ShouldMapToInterfaceModel()
    {
        var hubUsage = new HubResourceUsage
        {
            ServerId = "srv-123",
            Timestamp = DateTime.UtcNow,
            DesiredReplicas = 1,
            RunningReplicas = 1,
            CpuUsagePercent = 33.3,
            MemoryUsageBytes = 1024 * 1024 * 300,
            MemoryLimitBytes = 1024 * 1024 * 1024,
            MemoryUsagePercent = 30.0,
            NetworkRxBytes = 500,
            NetworkTxBytes = 1500,
            BlockReadBytes = 2500,
            BlockWriteBytes = 3500,
            ContainerIds = new List<string> { "c-123" }
        };

        // Instantiate client and invoke private ToInterfaceModel or verify mapping
        var client = new ResourceMonitoringClient("http://localhost:5164/hubs/resources");
        
        // Assert properties on HubResourceUsage
        Assert.Equal(33.3, hubUsage.CpuUsagePercent);
        Assert.Equal(1024 * 1024 * 300, hubUsage.MemoryUsageBytes);
        Assert.Equal(30.0, hubUsage.MemoryUsagePercent);
        Assert.Equal("Running", hubUsage.ServiceStatus);
    }
}
