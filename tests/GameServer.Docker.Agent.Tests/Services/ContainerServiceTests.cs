using Docker.DotNet;
using Docker.DotNet.Models;
using GameServer.Docker.Agent.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using DockerStatsResponse = Docker.DotNet.Models.ContainerStatsResponse;

namespace GameServer.Docker.Agent.Tests.Services;

public class ContainerServiceTests
{
    private readonly Mock<IDockerClient> _mockDockerClient;
    private readonly Mock<IContainerOperations> _mockContainerOperations;
    private readonly Mock<IExecOperations> _mockExecOperations;
    private readonly Mock<ILogger<ContainerService>> _mockLogger;
    private readonly ContainerService _service;

    public ContainerServiceTests()
    {
        _mockDockerClient = new Mock<IDockerClient>();
        _mockContainerOperations = new Mock<IContainerOperations>();
        _mockExecOperations = new Mock<IExecOperations>();
        _mockLogger = new Mock<ILogger<ContainerService>>();

        _mockDockerClient.SetupGet(x => x.Containers).Returns(_mockContainerOperations.Object);
        _mockDockerClient.SetupGet(x => x.Exec).Returns(_mockExecOperations.Object);

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
    public async Task InspectContainerAsync_ReturnsMappedResponse()
    {
        var inspect = new ContainerInspectResponse
        {
            Name = "/mc-server",
            Created = DateTime.UtcNow,
            Image = "itzg/minecraft",
            Platform = "linux",
            State = new()
            {
                Status = "running",
                Running = true,
                Pid = 1234,
                StartedAt = DateTime.UtcNow.ToString("o"),
                FinishedAt = DateTime.MinValue.ToString("o")
            }
        };

        _mockContainerOperations.Setup(c => c.InspectContainerAsync("c-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(inspect);

        var result = await _service.InspectContainerAsync("c-1");

        Assert.NotNull(result);
        Assert.Equal("c-1", result.ContainerId);
        Assert.Equal("/mc-server", result.Name);
        Assert.True(result.State.Running);
    }

    [Fact]
    public async Task ListContainersAsync_ReturnsMappedContainers()
    {
        var containers = new List<ContainerListResponse>
        {
            new() { ID = "c-1", Names = ["/mc-1"], Image = "img1", State = "running", Status = "Up 1 hour" }
        };

        _mockContainerOperations.Setup(c => c.ListContainersAsync(It.IsAny<ContainersListParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        var result = await _service.ListContainersAsync();

        Assert.NotNull(result);
        Assert.Equal(1, result.ContainerCount);
        Assert.Single(result.Containers);
        Assert.Equal("c-1", result.Containers[0].Id);
    }

    [Fact]
    public async Task SaveFileContentTextAsync_ExtractsArchiveToContainer()
    {
        _mockContainerOperations.Setup(c => c.ExtractArchiveToContainerAsync(
            "c-1",
            It.IsAny<CopyToContainerParameters>(),
            It.IsAny<Stream>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _service.SaveFileContentTextAsync("c-1", "/data/server.properties", "motd=Minecraft");

        _mockContainerOperations.Verify(c => c.ExtractArchiveToContainerAsync(
            "c-1",
            It.IsAny<CopyToContainerParameters>(),
            It.IsAny<Stream>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadFileAsync_ExtractsArchiveToContainer()
    {
        _mockContainerOperations.Setup(c => c.ExtractArchiveToContainerAsync(
            "c-1",
            It.IsAny<CopyToContainerParameters>(),
            It.IsAny<Stream>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        await _service.UploadFileAsync("c-1", "/data", "test.bin", stream);

        _mockContainerOperations.Verify(c => c.ExtractArchiveToContainerAsync(
            "c-1",
            It.IsAny<CopyToContainerParameters>(),
            It.IsAny<Stream>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetFileContentTextAsync_ReadsFromTarStream()
    {
        using var tarMs = new MemoryStream();
        using (var tarWriter = new System.Formats.Tar.TarWriter(tarMs, System.Formats.Tar.TarEntryFormat.Pax, leaveOpen: true))
        {
            var dataBytes = Encoding.UTF8.GetBytes("hello server properties");
            using var dataStream = new MemoryStream(dataBytes);
            var entry = new System.Formats.Tar.PaxTarEntry(System.Formats.Tar.TarEntryType.RegularFile, "server.properties")
            {
                DataStream = dataStream
            };
            await tarWriter.WriteEntryAsync(entry);
        }
        tarMs.Position = 0;

        _mockContainerOperations.Setup(c => c.GetArchiveFromContainerAsync(
            "c-1",
            It.IsAny<ContainerPathStatParameters>(),
            false,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContainerArchiveResponse { Stream = tarMs });

        var content = await _service.GetFileContentTextAsync("c-1", "/data/server.properties");
        Assert.Equal("hello server properties", content);
    }

    [Fact]
    public async Task GetFileStreamAsync_ReadsFromTarStream()
    {
        using var tarMs = new MemoryStream();
        using (var tarWriter = new System.Formats.Tar.TarWriter(tarMs, System.Formats.Tar.TarEntryFormat.Pax, leaveOpen: true))
        {
            var dataBytes = new byte[] { 10, 20, 30 };
            using var dataStream = new MemoryStream(dataBytes);
            var entry = new System.Formats.Tar.PaxTarEntry(System.Formats.Tar.TarEntryType.RegularFile, "world.zip")
            {
                DataStream = dataStream
            };
            await tarWriter.WriteEntryAsync(entry);
        }
        tarMs.Position = 0;

        _mockContainerOperations.Setup(c => c.GetArchiveFromContainerAsync(
            "c-1",
            It.IsAny<ContainerPathStatParameters>(),
            false,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContainerArchiveResponse { Stream = tarMs });

        var (stream, contentType, fileName) = await _service.GetFileStreamAsync("c-1", "/data/world.zip");
        Assert.Equal("world.zip", fileName);
        Assert.Equal(3, stream.Length);
    }

    [Fact]
    public async Task ListFilesAsync_WhenExecThrows_FallsBackToArchive()
    {
        _mockExecOperations.Setup(e => e.CreateContainerExecAsync("c-1", It.IsAny<ContainerExecCreateParameters>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Exec not supported"));

        using var tarMs = new MemoryStream();
        using (var tarWriter = new System.Formats.Tar.TarWriter(tarMs, System.Formats.Tar.TarEntryFormat.Pax, leaveOpen: true))
        {
            var entry1 = new System.Formats.Tar.PaxTarEntry(System.Formats.Tar.TarEntryType.Directory, "data");
            await tarWriter.WriteEntryAsync(entry1);

            var entry2 = new System.Formats.Tar.PaxTarEntry(System.Formats.Tar.TarEntryType.RegularFile, "data/file1.txt")
            {
                DataStream = new MemoryStream(new byte[10])
            };
            await tarWriter.WriteEntryAsync(entry2);
        }
        tarMs.Position = 0;

        _mockContainerOperations.Setup(c => c.GetArchiveFromContainerAsync(
            "c-1",
            It.IsAny<ContainerPathStatParameters>(),
            false,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContainerArchiveResponse { Stream = tarMs });

        var files = await _service.ListFilesAsync("c-1", "/data");
        Assert.NotNull(files);
        Assert.Single(files);
        Assert.Equal("file1.txt", files[0].Name);
    }
}
