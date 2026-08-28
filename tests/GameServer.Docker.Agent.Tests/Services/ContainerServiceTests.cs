using Docker.DotNet;
using Docker.DotNet.Models;
using GameServer.Docker.Agent.Services;
using Microsoft.Extensions.Logging;
using Moq;
using DockerStatsResponse = Docker.DotNet.Models.ContainerStatsResponse;

namespace GameServer.Docker.Agent.Tests.Services;

public class ContainerServiceTests
{
    private readonly Mock<IDockerClient> _mockDockerClient;
    private readonly Mock<IContainerOperations> _mockContainerOperations;
    private readonly Mock<ILogger<ContainerService>> _mockLogger;
    private readonly ContainerService _service;

    public ContainerServiceTests()
    {
        _mockDockerClient = new Mock<IDockerClient>();
        _mockContainerOperations = new Mock<IContainerOperations>();
        _mockLogger = new Mock<ILogger<ContainerService>>();

        _mockDockerClient.SetupGet(x => x.Containers).Returns(_mockContainerOperations.Object);

        _service = new ContainerService(_mockDockerClient.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetContainerStatsAsync_WhenDockerReturnsCompleteStats_ShouldReturnPopulatedStats()
    {
        // Arrange
        var containerId = "container-123";
        var dockerStats = new DockerStatsResponse
        {
            CPUStats = new CPUStats
            {
                CPUUsage = new CPUUsage
                {
                    TotalUsage = 200000000,
                    PercpuUsage = [100000000, 100000000]
                },
                SystemUsage = 1000000000,
                OnlineCPUs = 2
            },
            PreCPUStats = new CPUStats
            {
                CPUUsage = new CPUUsage { TotalUsage = 100000000 },
                SystemUsage = 500000000
            },
            MemoryStats = new MemoryStats
            {
                Usage = 512 * 1024 * 1024,
                Limit = 1024 * 1024 * 1024,
                MaxUsage = 600 * 1024 * 1024
            },
            Networks = new Dictionary<string, NetworkStats>
            {
                ["eth0"] = new NetworkStats { RxBytes = 1024, TxBytes = 2048 }
            },
            BlkioStats = new BlkioStats
            {
                IoServiceBytesRecursive =
                [
                    new BlkioStatEntry { Op = "read", Value = 4096 },
                    new BlkioStatEntry { Op = "write", Value = 8192 }
                ]
            },
            PidsStats = new PidsStats { Current = 5 }
        };

        _mockContainerOperations
            .Setup(x => x.GetContainerStatsAsync(
                containerId,
                It.IsAny<ContainerStatsParameters>(),
                It.IsAny<IProgress<DockerStatsResponse>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, ContainerStatsParameters, IProgress<DockerStatsResponse>, CancellationToken>(
                (id, param, progress, token) =>
                {
                    progress.Report(dockerStats);
                })
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.GetContainerStatsAsync(containerId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(containerId, result.ContainerId);
        Assert.Equal(40.0, result.Cpu.UsagePercent); // (200M - 100M) / (1000M - 500M) * 2 cpus * 100 = 40%
        Assert.Equal((ulong)200000000, result.Cpu.TotalUsage);
        Assert.Equal((ulong)1000000000, result.Cpu.SystemUsage);
        Assert.Equal((ulong)2, result.Cpu.OnlineCpus);
        Assert.Equal((ulong)512 * 1024 * 1024, result.Memory.UsageBytes);
        Assert.Equal((ulong)1024 * 1024 * 1024, result.Memory.LimitBytes);
        Assert.Equal(50.0, result.Memory.UsagePercent);
        Assert.Equal(1024, result.Network.RxBytes);
        Assert.Equal(2048, result.Network.TxBytes);
        Assert.Equal(4096, result.BlockIo.ReadBytes);
        Assert.Equal(8192, result.BlockIo.WriteBytes);
        Assert.Equal((ulong)5, result.Pids);
    }

    [Fact]
    public async Task GetContainerStatsAsync_WhenDockerReturnsNullAndEmptyNestedFields_ShouldNotThrowNullReference()
    {
        // Arrange: Docker returns a container stats object with null sub-properties
        var containerId = "container-nulls";
        var dockerStats = new DockerStatsResponse
        {
            CPUStats = null!,
            PreCPUStats = null!,
            MemoryStats = null!,
            Networks = null!,
            BlkioStats = null!,
            PidsStats = null!
        };

        _mockContainerOperations
            .Setup(x => x.GetContainerStatsAsync(
                containerId,
                It.IsAny<ContainerStatsParameters>(),
                It.IsAny<IProgress<DockerStatsResponse>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, ContainerStatsParameters, IProgress<DockerStatsResponse>, CancellationToken>(
                (id, param, progress, token) =>
                {
                    progress.Report(dockerStats);
                })
            .Returns(Task.CompletedTask);

        // Act & Assert (Must not throw NullReferenceException)
        var result = await _service.GetContainerStatsAsync(containerId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(containerId, result.ContainerId);
        Assert.Equal(0.0, result.Cpu.UsagePercent);
        Assert.Equal((ulong)0, result.Memory.UsageBytes);
        Assert.Equal(0, result.Network.RxBytes);
        Assert.Equal(0, result.BlockIo.ReadBytes);
        Assert.Equal((ulong)0, result.Pids);
    }

    [Fact]
    public async Task GetContainerStatsAsync_WhenDockerReturnsPartiallyInitializedCpu_ShouldNotThrow()
    {
        // Arrange: Docker returns CPUStats without CPUUsage or SystemUsage
        var containerId = "container-partial";
        var dockerStats = new DockerStatsResponse
        {
            CPUStats = new CPUStats
            {
                CPUUsage = null!,
                SystemUsage = null,
                OnlineCPUs = null
            },
            PreCPUStats = new CPUStats
            {
                CPUUsage = null!,
                SystemUsage = null
            },
            MemoryStats = new MemoryStats
            {
                Usage = null,
                Limit = null
            }
        };

        _mockContainerOperations
            .Setup(x => x.GetContainerStatsAsync(
                containerId,
                It.IsAny<ContainerStatsParameters>(),
                It.IsAny<IProgress<DockerStatsResponse>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, ContainerStatsParameters, IProgress<DockerStatsResponse>, CancellationToken>(
                (id, param, progress, token) =>
                {
                    progress.Report(dockerStats);
                })
            .Returns(Task.CompletedTask);

        // Act & Assert
        var result = await _service.GetContainerStatsAsync(containerId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(containerId, result.ContainerId);
        Assert.Equal(0.0, result.Cpu.UsagePercent);
        Assert.Equal((ulong)0, result.Memory.UsageBytes);
    }
}
