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

    [Fact]
    public async Task SaveFileContentTextAsync_ExtractsArchiveToParentDirectory()
    {
        var containerId = "cnt-save-1";
        var filePath = "/home/steam/server/config.json";
        CopyToContainerParameters? capturedParams = null;

        _mockContainerOperations
            .Setup(x => x.ExtractArchiveToContainerAsync(
                containerId,
                It.IsAny<CopyToContainerParameters>(),
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, CopyToContainerParameters, Stream, CancellationToken>(
                (id, p, s, t) => capturedParams = p)
            .Returns(Task.CompletedTask);

        await _service.SaveFileContentTextAsync(containerId, filePath, "{\"key\":\"value\"}", CancellationToken.None);

        Assert.NotNull(capturedParams);
        Assert.Equal("/home/steam/server", capturedParams.Path);
    }

    [Fact]
    public async Task UploadFileAsync_ExtractsArchiveToTargetDirectory()
    {
        var containerId = "cnt-upload-1";
        var dirPath = "/home/steam/server/mods";
        var fileName = "mod.dll";
        using var stream = new MemoryStream([1, 2, 3]);
        CopyToContainerParameters? capturedParams = null;

        _mockContainerOperations
            .Setup(x => x.ExtractArchiveToContainerAsync(
                containerId,
                It.IsAny<CopyToContainerParameters>(),
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, CopyToContainerParameters, Stream, CancellationToken>(
                (id, p, s, t) => capturedParams = p)
            .Returns(Task.CompletedTask);

        await _service.UploadFileAsync(containerId, dirPath, fileName, stream, CancellationToken.None);

        Assert.NotNull(capturedParams);
        Assert.Equal("/home/steam/server/mods", capturedParams.Path);
    }

    [Fact]
    public async Task CreateDirectoryAsync_ExtractsArchiveToParentDirectory()
    {
        var containerId = "cnt-mkdir-1";
        var dirPath = "/home/steam/server/BepInEx/plugins";
        CopyToContainerParameters? capturedParams = null;

        _mockContainerOperations
            .Setup(x => x.ExtractArchiveToContainerAsync(
                containerId,
                It.IsAny<CopyToContainerParameters>(),
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, CopyToContainerParameters, Stream, CancellationToken>(
                (id, p, s, t) => capturedParams = p)
            .Returns(Task.CompletedTask);

        await _service.CreateDirectoryAsync(containerId, dirPath, CancellationToken.None);

        Assert.NotNull(capturedParams);
        Assert.Equal("/home/steam/server/BepInEx", capturedParams.Path);
    }

    [Fact]
    public async Task GetFileContentTextAsync_WhenArchiveContainsFile_ReturnsContent()
    {
        var containerId = "cnt-get-file-1";
        var filePath = "/home/steam/aska_server/doorstop_config.ini";
        var expectedContent = "[General]\nenabled=true";
        var tarStream = CreateTarArchiveWithFile("doorstop_config.ini", expectedContent);

        // Exec mock fails so it falls back to archive
        var mockExecOps = new Mock<IExecOperations>();
        _mockDockerClient.SetupGet(x => x.Exec).Returns(mockExecOps.Object);
        mockExecOps
            .Setup(x => x.CreateContainerExecAsync(It.IsAny<string>(), It.IsAny<ContainerExecCreateParameters>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Exec not available"));

        _mockContainerOperations
            .Setup(x => x.GetArchiveFromContainerAsync(
                containerId,
                It.IsAny<ContainerPathStatParameters>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContainerArchiveResponse { Stream = tarStream });

        var content = await _service.GetFileContentTextAsync(containerId, filePath, CancellationToken.None);

        Assert.Equal(expectedContent, content);
    }

    [Fact]
    public async Task GetFileStreamAsync_WhenArchiveContainsFile_ReturnsStream()
    {
        var containerId = "cnt-get-stream-1";
        var filePath = "/home/steam/aska_server/Doorstop_LICENSE.txt";
        var expectedContent = "MIT License";
        var tarStream = CreateTarArchiveWithFile("Doorstop_LICENSE.txt", expectedContent);

        _mockContainerOperations
            .Setup(x => x.GetArchiveFromContainerAsync(
                containerId,
                It.IsAny<ContainerPathStatParameters>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContainerArchiveResponse { Stream = tarStream });

        var (stream, contentType, fileName) = await _service.GetFileStreamAsync(containerId, filePath, CancellationToken.None);

        Assert.NotNull(stream);
        Assert.Equal("Doorstop_LICENSE.txt", fileName);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        Assert.Equal(expectedContent, content);
    }

    [Fact]
    public async Task GetFileContentTextAsync_WhenDockerReturns404_ThrowsFileNotFoundException()
    {
        var containerId = "cnt-not-found-1";
        var filePath = "/home/steam/nonexistent.txt";

        var mockExecOps = new Mock<IExecOperations>();
        _mockDockerClient.SetupGet(x => x.Exec).Returns(mockExecOps.Object);
        mockExecOps
            .Setup(x => x.CreateContainerExecAsync(It.IsAny<string>(), It.IsAny<ContainerExecCreateParameters>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Exec not available"));

        _mockContainerOperations
            .Setup(x => x.GetArchiveFromContainerAsync(
                containerId,
                It.IsAny<ContainerPathStatParameters>(),
                false,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DockerApiException(System.Net.HttpStatusCode.NotFound, "Could not find the file"));

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _service.GetFileContentTextAsync(containerId, filePath, CancellationToken.None));
    }

    private static Stream CreateTarArchiveWithFile(string fileName, string content)
    {
        var contentBytes = System.Text.Encoding.UTF8.GetBytes(content);
        var tarMs = new MemoryStream();
        using (var tarWriter = new System.Formats.Tar.TarWriter(tarMs, System.Formats.Tar.TarEntryFormat.Pax, leaveOpen: true))
        {
            var entry = new System.Formats.Tar.PaxTarEntry(System.Formats.Tar.TarEntryType.RegularFile, fileName)
            {
                DataStream = new MemoryStream(contentBytes),
                Mode = (UnixFileMode)0644
            };
            tarWriter.WriteEntry(entry);
        }
        tarMs.Position = 0;
        return tarMs;
    }
}
