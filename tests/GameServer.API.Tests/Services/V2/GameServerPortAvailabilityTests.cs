using Docker.DotNet.Models;
using GameServer.API.Configurations;
using GameServer.API.Constants;
using GameServer.API.Dtos.V2;
using GameServer.API.Interfaces;
using GameServer.API.Models.V2;
using GameServer.API.Repositories.V2;
using GameServer.API.Services.V2;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GameServer.API.Tests.Services.V2;

public class GameServerPortAvailabilityTests
{
    [Fact]
    public async Task CheckPortAvailabilityAsync_WhenPortIsFree_ShouldReportAvailable()
    {
        // Arrange
        var service = CreateService([]);

        // Act
        var result = await service.CheckPortAvailabilityAsync(BuildRequest("server-a", (1, 25565, "tcp")));

        // Assert
        var port = Assert.Single(result.Ports);
        Assert.True(port.IsAvailable);
        Assert.Null(port.Reason);
        Assert.Equal(1, port.PortId);
    }

    [Fact]
    public async Task CheckPortAvailabilityAsync_WhenPortUsedByAnotherServer_ShouldReportUnavailable()
    {
        // Arrange
        var service = CreateService([CreateService("other-server", 25565, "tcp")]);

        // Act
        var result = await service.CheckPortAvailabilityAsync(BuildRequest("server-a", (1, 25565, "tcp")));

        // Assert
        var port = Assert.Single(result.Ports);
        Assert.False(port.IsAvailable);
        Assert.Contains("already in use", port.Reason);
    }

    [Fact]
    public async Task CheckPortAvailabilityAsync_WhenPortUsedByTheSameServer_ShouldReportAvailable()
    {
        // Arrange
        var service = CreateService([CreateService("server-a", 25565, "tcp")]);

        // Act
        var result = await service.CheckPortAvailabilityAsync(BuildRequest("server-a", (1, 25565, "tcp")));

        // Assert
        var port = Assert.Single(result.Ports);
        Assert.True(port.IsAvailable);
    }

    [Fact]
    public async Task CheckPortAvailabilityAsync_WhenPortOutsideAllocationRange_ShouldReportUnavailable()
    {
        // Arrange
        var service = CreateService([]);

        // Act
        var result = await service.CheckPortAvailabilityAsync(BuildRequest("server-a", (1, 80, "tcp")));

        // Assert
        var port = Assert.Single(result.Ports);
        Assert.False(port.IsAvailable);
        Assert.Contains("allocation range", port.Reason);
    }

    [Fact]
    public async Task CheckPortAvailabilityAsync_WhenSamePortRequestedTwice_ShouldReportDuplicate()
    {
        // Arrange
        var service = CreateService([]);

        // Act
        var result = await service.CheckPortAvailabilityAsync(BuildRequest("server-a", (1, 25565, "tcp"), (2, 25565, "tcp")));

        // Assert
        Assert.Equal(2, result.Ports.Count);
        Assert.All(result.Ports, port =>
        {
            Assert.False(port.IsAvailable);
            Assert.Contains("more than once", port.Reason);
        });
    }

    [Fact]
    public async Task CheckPortAvailabilityAsync_WhenNoPortsRequested_ShouldReturnEmptyResult()
    {
        // Arrange
        var service = CreateService([]);

        // Act
        var result = await service.CheckPortAvailabilityAsync(new GameServerPortAvailabilityRequestDto { ServerId = "server-a" });

        // Assert
        Assert.Empty(result.Ports);
    }

    private static GameServerPortAvailabilityRequestDto BuildRequest(string serverId, params (int PortId, int Port, string Protocol)[] ports)
    {
        return new GameServerPortAvailabilityRequestDto
        {
            ServerId = serverId,
            Ports = ports
                .Select(port => new GameServerPortAvailabilityRequestPortDto
                {
                    PortId = port.PortId,
                    Port = port.Port,
                    Protocol = port.Protocol
                })
                .ToList()
        };
    }

    private static SwarmService CreateService(string serverId, uint publishedPort, string protocol)
    {
        return new SwarmService
        {
            Spec = new ServiceSpec
            {
                Labels = new Dictionary<string, string>
                {
                    [ServiceLabels.Managed] = ServiceLabels.ManagedValue,
                    [ServiceLabels.ServerId] = serverId
                }
            },
            Endpoint = new Endpoint
            {
                Ports =
                [
                    new PortConfig { PublishedPort = publishedPort, TargetPort = publishedPort, Protocol = protocol }
                ]
            }
        };
    }

    [Fact]
    public async Task CheckPortAvailabilityAsync_WhenPortIsReserved_ShouldReportUnavailable()
    {
        // Arrange
        var portAllocation = new PortAllocation
        {
            StartPort = 2000,
            EndPort = 100000,
            ReservedPortRanges = ["22,6666,9443,8000-9002,11434"]
        };
        var service = CreateService([], portAllocation);

        // Act - Port in range 8000-9002
        var result = await service.CheckPortAvailabilityAsync(BuildRequest("server-a", (1, 8080, "tcp"), (2, 9002, "tcp"), (3, 6666, "tcp"), (4, 25565, "tcp")));

        // Assert
        Assert.Equal(4, result.Ports.Count);
        Assert.False(result.Ports[0].IsAvailable);
        Assert.Contains("reserved", result.Ports[0].Reason);

        Assert.False(result.Ports[1].IsAvailable);
        Assert.Contains("reserved", result.Ports[1].Reason);

        Assert.False(result.Ports[2].IsAvailable);
        Assert.Contains("reserved", result.Ports[2].Reason);

        Assert.True(result.Ports[3].IsAvailable);
        Assert.Null(result.Ports[3].Reason);
    }

    private static GameServerValidationService CreateService(IList<SwarmService> services, PortAllocation? portAllocation = null)
    {
        var serviceOperations = new Mock<IServiceOperations>();
        serviceOperations
            .Setup(x => x.ListServicesAsync($"{ServiceLabels.Managed}={ServiceLabels.ManagedValue}", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        return new GameServerValidationService(
            Mock.Of<IGameTypeRepository>(),
            serviceOperations.Object,
            portAllocation ?? new PortAllocation { StartPort = 2000, EndPort = 100000 },
            new VolumeSetupResolver(Mock.Of<IMountTypeConfigRepository>(), Mock.Of<GameServer.API.Services.V2.MountTypeHandlers.IMountTypeHandlerFactory>(), NullLogger<VolumeSetupResolver>.Instance),
            Mock.Of<IMountTypeConfigRepository>());
    }
}
