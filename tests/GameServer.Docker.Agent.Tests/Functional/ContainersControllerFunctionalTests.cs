using GameServer.Docker.Agent.Controllers;
using GameServer.Docker.Agent.Models;
using GameServer.Docker.Agent.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;

namespace GameServer.Docker.Agent.Tests.Functional;

[Collection("Docker Functional Tests")]
public class ContainersControllerFunctionalTests
{
    private readonly DockerTestFixture _fixture;
    private readonly ContainersController? _controller;

    public ContainersControllerFunctionalTests(DockerTestFixture fixture)
    {
        _fixture = fixture;
        if (_fixture.IsDockerAvailable && _fixture.DockerClient != null)
        {
            var containerService = new ContainerService(_fixture.DockerClient, NullLogger<ContainerService>.Instance);
            _controller = new ContainersController(containerService, NullLogger<ContainersController>.Instance)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };
        }
    }

    [Fact]
    public async Task GetContainerStats_WithLiveContainer_ReturnsOk()
    {
        if (!_fixture.IsDockerAvailable || _controller == null || _fixture.TestContainerId == null)
            return;

        var result = await _controller.GetContainerStats(_fixture.TestContainerId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var stats = Assert.IsType<ContainerStatsResponse>(okResult.Value);
        Assert.Equal(_fixture.TestContainerId, stats.ContainerId);
    }

    [Fact]
    public async Task GetContainerLogs_WithLiveContainer_ReturnsOk()
    {
        if (!_fixture.IsDockerAvailable || _controller == null || _fixture.TestContainerId == null)
            return;

        var result = await _controller.GetContainerLogs(_fixture.TestContainerId, tail: 20, cancellationToken: CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var logs = Assert.IsType<ContainerLogsResponse>(okResult.Value);
        Assert.Equal(_fixture.TestContainerId, logs.ContainerId);
    }

    [Fact]
    public async Task ListContainers_WithLiveContainer_ReturnsOk()
    {
        if (!_fixture.IsDockerAvailable || _controller == null || _fixture.TestContainerId == null)
            return;

        var result = await _controller.ListContainers(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsType<ContainerListResponse>(okResult.Value);
        Assert.True(list.ContainerCount > 0);
    }

    [Fact]
    public async Task InspectContainer_WithLiveContainer_ReturnsOk()
    {
        if (!_fixture.IsDockerAvailable || _controller == null || _fixture.TestContainerId == null)
            return;

        var result = await _controller.InspectContainer(_fixture.TestContainerId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var inspect = Assert.IsType<ContainerInspectResponse>(okResult.Value);
        Assert.Equal(_fixture.TestContainerId, inspect.ContainerId);
    }

    [Fact]
    public async Task FileEndpoints_WithLiveContainer_PerformFullLifecycle()
    {
        if (!_fixture.IsDockerAvailable || _controller == null || _fixture.TestContainerId == null)
            return;

        var containerId = _fixture.TestContainerId;
        var filePath = "/tmp/controller-test.txt";
        var content = "Testing via controller: " + Guid.NewGuid();

        // 1. Save text file
        var saveResult = await _controller.SaveFileContent(
            containerId,
            filePath,
            new SaveFileRequest { Content = content },
            CancellationToken.None);
        Assert.IsType<OkResult>(saveResult);

        // 2. Read file content
        var readResult = await _controller.GetFileContent(containerId, filePath, CancellationToken.None);
        var okRead = Assert.IsType<OkObjectResult>(readResult);
        Assert.Equal(content, okRead.Value);

        // 3. List files
        var listFilesResult = await _controller.ListFiles(containerId, "/tmp", CancellationToken.None);
        var okList = Assert.IsType<OkObjectResult>(listFilesResult);
        var files = Assert.IsAssignableFrom<IReadOnlyList<ContainerFileItemResponse>>(okList.Value);
        Assert.Contains(files, f => f.Name == "controller-test.txt");

        // 4. Download file
        var downloadResult = await _controller.DownloadFile(containerId, filePath, CancellationToken.None);
        var fileStreamResult = Assert.IsType<FileStreamResult>(downloadResult);
        using (fileStreamResult.FileStream)
        {
            using var reader = new StreamReader(fileStreamResult.FileStream, Encoding.UTF8);
            var downloaded = await reader.ReadToEndAsync();
            Assert.Equal(content, downloaded);
        }

        // 5. Delete file
        var deleteResult = await _controller.DeleteFileOrDirectory(containerId, filePath, recursive: false, cancellationToken: CancellationToken.None);
        Assert.IsType<OkResult>(deleteResult);
    }
}
