using Docker.DotNet.Models;
using GameServer.Docker.Configurations;
using GameServer.Docker.Constants;
using GameServer.Docker.Dtos.V2;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Models.V2;
using GameServer.Docker.Repositories.V2;
using GameServer.Docker.Services.V2;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GameServer.Docker.Tests.Services.V2;

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

    private static GameServerValidationService CreateService(IList<SwarmService> services)
    {
        var serviceOperations = new Mock<IServiceOperations>();
        serviceOperations
            .Setup(x => x.ListServicesAsync($"{ServiceLabels.Managed}={ServiceLabels.ManagedValue}", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        return new GameServerValidationService(
            Mock.Of<IGameTypeRepository>(),
            serviceOperations.Object,
            new PortAllocation { StartPort = 2000, EndPort = 100000 },
            new VolumeSetupResolver(Mock.Of<IMountTypeConfigRepository>(), Mock.Of<GameServer.Docker.Services.V2.MountTypeHandlers.IMountTypeHandlerFactory>(), NullLogger<VolumeSetupResolver>.Instance),
            Mock.Of<IMountTypeConfigRepository>());
    }
}
