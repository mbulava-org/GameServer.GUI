using System.Net;
using System.Text.Json;
using GameServer.API.Dtos.V2;
using GameServer.API.Interfaces;
using GameServer.API.Models;
using GameServer.API.Models.V2;
using GameServer.API.Repositories.V2;
using GameServer.API.Services.V2;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace GameServer.API.Tests.Services.V2;

public class GameServerFilesServiceTests
{
    private readonly Mock<IGameServerRepository> _gameServerRepoMock;
    private readonly Mock<INodeAgentDiscovery> _nodeAgentDiscoveryMock;
    private readonly Mock<IServerResourceMonitor> _serverResourceMonitorMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly GameServerFilesService _service;

    public GameServerFilesServiceTests()
    {
        _gameServerRepoMock = new Mock<IGameServerRepository>();
        _nodeAgentDiscoveryMock = new Mock<INodeAgentDiscovery>();
        _serverResourceMonitorMock = new Mock<IServerResourceMonitor>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _httpHandlerMock = new Mock<HttpMessageHandler>();

        var httpClient = new HttpClient(_httpHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        _service = new GameServerFilesService(
            _gameServerRepoMock.Object,
            _nodeAgentDiscoveryMock.Object,
            _httpClientFactoryMock.Object,
            NullLogger<GameServerFilesService>.Instance,
            _serverResourceMonitorMock.Object);
    }

    private void SetupActiveServer(string serverId, string containerId, string agentUrl)
    {
        var usage = new ServerResourceUsage
        {
            ServerId = serverId,
            ContainerIds = [containerId]
        };

        _serverResourceMonitorMock.Setup(m => m.GetSnapshotAsync(serverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usage);

        var endpoint = new NodeAgentEndpoint
        {
            NodeId = "node-1",
            NodeName = "worker-1",
            InternalUrl = agentUrl
        };

        _nodeAgentDiscoveryMock.Setup(d => d.GetAgentForContainerAsync(containerId))
            .ReturnsAsync(endpoint);
        _nodeAgentDiscoveryMock.Setup(d => d.GetAgentForServerAsync(serverId))
            .ReturnsAsync(endpoint);
    }

    [Fact]
    public async Task ListFilesAsync_WhenServerIsRunning_QueriesAgentAndReturnsFiles()
    {
        const string serverId = "srv-123";
        const string containerId = "cnt-123";
        const string agentUrl = "http://node1:5000";
        SetupActiveServer(serverId, containerId, agentUrl);

        var expectedFiles = new List<FileItemDto>
        {
            new() { Name = "server.properties", Path = "/data/server.properties", IsDirectory = false, Size = 128 },
            new() { Name = "worlds", Path = "/data/worlds", IsDirectory = true }
        };

        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri!.ToString().Contains("/containers/cnt-123/files")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(expectedFiles))
            });

        var items = await _service.ListFilesAsync(serverId, "/data");

        Assert.NotNull(items);
        Assert.Equal(2, items.Count);
        Assert.Contains(items, i => i.Name == "server.properties");
        Assert.Contains(items, i => i.Name == "worlds" && i.IsDirectory);
    }

    [Fact]
    public async Task ListFilesAsync_WhenServerIsNotRunning_ReturnsEmptyList()
    {
        const string serverId = "srv-stopped";
        _serverResourceMonitorMock.Setup(m => m.GetSnapshotAsync(serverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServerResourceUsage?)null);
        _nodeAgentDiscoveryMock.Setup(d => d.GetAgentForServerAsync(serverId))
            .ReturnsAsync((NodeAgentEndpoint?)null);

        var items = await _service.ListFilesAsync(serverId, "/data");

        Assert.NotNull(items);
        Assert.Empty(items);
    }

    [Fact]
    public async Task GetFileContentTextAsync_WhenServerIsRunning_ReturnsContent()
    {
        const string serverId = "srv-456";
        const string containerId = "cnt-456";
        const string agentUrl = "http://node1:5000";
        SetupActiveServer(serverId, containerId, agentUrl);

        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri!.ToString().Contains("/content")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("motd=Welcome")
            });

        var content = await _service.GetFileContentTextAsync(serverId, "/data", "server.properties");
        Assert.Equal("motd=Welcome", content);
    }

    [Fact]
    public async Task GetFileContentTextAsync_WhenServerIsNotRunning_ThrowsInvalidOperationException()
    {
        const string serverId = "srv-stopped";
        _serverResourceMonitorMock.Setup(m => m.GetSnapshotAsync(serverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServerResourceUsage?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.GetFileContentTextAsync(serverId, "/data", "server.properties"));
    }

    [Fact]
    public async Task SaveFileContentTextAsync_WhenServerIsRunning_PutsToAgent()
    {
        const string serverId = "srv-789";
        const string containerId = "cnt-789";
        const string agentUrl = "http://node1:5000";
        SetupActiveServer(serverId, containerId, agentUrl);

        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Put && req.RequestUri!.ToString().Contains("/content")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        await _service.SaveFileContentTextAsync(serverId, "/data", "config.yml", "test: true");
    }

    [Fact]
    public async Task CreateDirectoryAsync_WhenServerIsRunning_PostsToAgent()
    {
        const string serverId = "srv-dir";
        const string containerId = "cnt-dir";
        const string agentUrl = "http://node1:5000";
        SetupActiveServer(serverId, containerId, agentUrl);

        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post && req.RequestUri!.ToString().Contains("/directory")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        await _service.CreateDirectoryAsync(serverId, "/data", "plugins");
    }
}
