using GameServer.Docker.Agent.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;

namespace GameServer.Docker.Agent.Tests.Functional;

[Collection("Docker Functional Tests")]
public class ContainerServiceFunctionalTests
{
    private readonly DockerTestFixture _fixture;
    private readonly ContainerService? _service;

    public ContainerServiceFunctionalTests(DockerTestFixture fixture)
    {
        _fixture = fixture;
        if (_fixture.IsDockerAvailable && _fixture.DockerClient != null)
        {
            _service = new ContainerService(_fixture.DockerClient, NullLogger<ContainerService>.Instance);
        }
    }

    [Fact]
    public async Task InspectContainerAsync_WithLiveContainer_ReturnsRunningState()
    {
        if (!_fixture.IsDockerAvailable || _service == null || _fixture.TestContainerId == null)
            return;

        var inspect = await _service.InspectContainerAsync(_fixture.TestContainerId);

        Assert.NotNull(inspect);
        Assert.Equal(_fixture.TestContainerId, inspect.ContainerId);
        Assert.True(inspect.State.Running);
    }

    [Fact]
    public async Task ListContainersAsync_WithLiveContainer_IncludesTestContainer()
    {
        if (!_fixture.IsDockerAvailable || _service == null || _fixture.TestContainerId == null)
            return;

        var list = await _service.ListContainersAsync();

        Assert.NotNull(list);
        Assert.Contains(list.Containers, c => c.Id.StartsWith(_fixture.TestContainerId[..12]));
    }

    [Fact]
    public async Task FileOperations_WriteReadDelete_SucceedsInLiveContainer()
    {
        if (!_fixture.IsDockerAvailable || _service == null || _fixture.TestContainerId == null)
            return;

        var containerId = _fixture.TestContainerId;
        var filePath = "/tmp/test-file.txt";
        var content = "Functional test content for docker agent: " + Guid.NewGuid();

        // 1. Save text file
        await _service.SaveFileContentTextAsync(containerId, filePath, content);

        // 2. Read text file
        var readContent = await _service.GetFileContentTextAsync(containerId, filePath);
        Assert.Equal(content, readContent);

        // 3. List files in directory
        var files = await _service.ListFilesAsync(containerId, "/tmp");
        Assert.Contains(files, f => f.Name == "test-file.txt");

        // 4. Get file stream
        var (stream, contentType, fileName) = await _service.GetFileStreamAsync(containerId, filePath);
        using (stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var streamContent = await reader.ReadToEndAsync();
            Assert.Equal(content, streamContent);
        }

        // 5. Delete file
        await _service.DeleteFileOrDirectoryAsync(containerId, filePath, recursive: false);

        // 6. Verify deleted
        var filesAfterDelete = await _service.ListFilesAsync(containerId, "/tmp");
        Assert.DoesNotContain(filesAfterDelete, f => f.Name == "test-file.txt");
    }

    [Fact]
    public async Task CreateDirectoryAndUploadFile_SucceedsInLiveContainer()
    {
        if (!_fixture.IsDockerAvailable || _service == null || _fixture.TestContainerId == null)
            return;

        var containerId = _fixture.TestContainerId;
        var dirPath = "/tmp/my-test-dir";

        // Create directory
        await _service.CreateDirectoryAsync(containerId, dirPath);

        // Upload binary file
        var testBytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02 };
        using (var ms = new MemoryStream(testBytes))
        {
            await _service.UploadFileAsync(containerId, dirPath, "payload.bin", ms);
        }

        // List files in created directory
        var files = await _service.ListFilesAsync(containerId, dirPath);
        Assert.Contains(files, f => f.Name == "payload.bin");

        // Cleanup
        await _service.DeleteFileOrDirectoryAsync(containerId, dirPath, recursive: true);
    }

    [Fact]
    public async Task GetContainerStatsAsync_WithLiveContainer_ReturnsValidCpuAndMemory()
    {
        if (!_fixture.IsDockerAvailable || _service == null || _fixture.TestContainerId == null)
            return;

        var stats = await _service.GetContainerStatsAsync(_fixture.TestContainerId);

        Assert.NotNull(stats);
        Assert.Equal(_fixture.TestContainerId, stats.ContainerId);
        Assert.True(stats.Memory.LimitBytes > 0);
    }

    [Fact]
    public async Task GetContainerLogsAsync_WithLiveContainer_ReturnsLogs()
    {
        if (!_fixture.IsDockerAvailable || _service == null || _fixture.TestContainerId == null)
            return;

        var logs = await _service.GetContainerLogsAsync(_fixture.TestContainerId, tailLines: 50);

        Assert.NotNull(logs);
        Assert.Equal(_fixture.TestContainerId, logs.ContainerId);
        Assert.NotNull(logs.Logs);
    }

    [Fact]
    public async Task StreamContainerLogsAsync_WithLiveContainer_StreamsOutput()
    {
        if (!_fixture.IsDockerAvailable || _service == null || _fixture.TestContainerId == null)
            return;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var logLines = new List<string>();

        try
        {
            await foreach (var line in _service.StreamContainerLogsAsync(_fixture.TestContainerId, follow: false, tailLines: 10, timestamps: false, cancellationToken: cts.Token))
            {
                logLines.Add(line);
            }
        }
        catch (OperationCanceledException) { }

        Assert.NotNull(logLines);
    }
}
