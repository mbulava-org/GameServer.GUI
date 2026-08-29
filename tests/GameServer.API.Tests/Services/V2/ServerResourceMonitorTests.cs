using Docker.DotNet.Models;
using GameServer.API.Interfaces;
using GameServer.API.Models;
using GameServer.API.Repositories.V2;
using GameServer.API.Services.V2;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using GameServerModel = GameServer.API.Models.V2.GameServer;

namespace GameServer.API.Tests.Services.V2;

public class ServerResourceMonitorTests
{
    [Theory]
    [InlineData(0, 0, 0, 0, 0, "Stopped")]
    [InlineData(1, 1, 0, 0, 0, "Running")]
    [InlineData(1, 0, 1, 0, 0, "Preparing")]
    [InlineData(1, 0, 0, 1, 0, "Starting")]
    [InlineData(1, 0, 0, 0, 1, "Failed")]
    [InlineData(2, 1, 0, 0, 0, "Starting")]
    [InlineData(1, 2, 0, 0, 0, "Scaling Down")]
    public void ServiceStatus_WhenVariousTaskCounts_ShouldReturnAccurateStatus(
        int desiredReplicas,
        int runningReplicas,
        int preparingTasks,
        int startingTasks,
        int failedTasks,
        string expectedStatus)
    {
        var usage = new ServerResourceUsage
        {
            DesiredReplicas = desiredReplicas,
            RunningReplicas = runningReplicas,
            PreparingTasks = preparingTasks,
            StartingTasks = startingTasks,
            FailedTasks = failedTasks
        };

        Assert.Equal(expectedStatus, usage.ServiceStatus);
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenTasksArePreparing_ShouldReturnPreparingStatus()
    {
        // Arrange
        var serverId = "monitor-srv-1";
        var serviceName = $"gameserver-{serverId}";
        var server = new GameServerModel
        {
            Id = 1,
            ServerId = serverId,
            Name = "Monitor Server",
            ServiceName = serviceName,
            GameTypeRevisionId = 1,
            Status = "Preparing",
            Settings = [],
            Ports = []
        };

        var serverRepo = new Mock<IGameServerRepository>();
        serverRepo.Setup(x => x.GetByServerIdAsync(serverId)).ReturnsAsync(server);

        var gameTypeRepo = new Mock<IGameTypeRepository>();
        gameTypeRepo.Setup(x => x.GetAllAsync(It.IsAny<bool>())).ReturnsAsync([]);
        var queryService = new GameServerQueryService(serverRepo.Object, gameTypeRepo.Object);

        var serviceOperations = new Mock<IServiceOperations>();
        serviceOperations
            .Setup(x => x.ListServicesAsync(null, serviceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new SwarmService
                {
                    ID = "swarm-svc-1",
                    Spec = new ServiceSpec
                    {
                        Name = serviceName,
                        Mode = new ServiceMode { Replicated = new ReplicatedService { Replicas = 1 } }
                    }
                }
            ]);

        serviceOperations
            .Setup(x => x.ListTasksAsync(It.IsAny<TasksListParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new TaskResponse
                {
                    ID = "task-prep-1",
                    Status = new Docker.DotNet.Models.TaskStatus
                    {
                        State = TaskState.Preparing,
                        Message = "pulling image..."
                    }
                }
            ]);

        var nodeAgentDiscovery = new Mock<INodeAgentDiscovery>();

        var monitor = new ServerResourceMonitor(
            serviceOperations.Object,
            nodeAgentDiscovery.Object,
            queryService,
            NullLogger<ServerResourceMonitor>.Instance);

        // Act
        var snapshot = await monitor.GetSnapshotAsync(serverId);

        // Assert
        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot.PreparingTasks);
        Assert.Equal(0, snapshot.RunningReplicas);
        Assert.Equal("Preparing", snapshot.ServiceStatus);
        Assert.Equal("Preparing", snapshot.LatestTaskState);
        Assert.Equal("pulling image...", snapshot.LatestTaskMessage);
    }
}
