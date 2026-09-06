using GameServer.API.Dtos.V2;
using GameServer.API.Interfaces;
using GameServer.API.Models;
using GameServer.API.Repositories.V2;
using GameServer.API.Services.V2;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;

namespace GameServer.API.Tests.Services.V2
{
    public class GameServerFilesServiceTests
    {
        private readonly Mock<IGameServerRepository> _repoMock;
        private readonly Mock<INodeAgentDiscovery> _discoveryMock;
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<ILogger<GameServerFilesService>> _loggerMock;
        private readonly Mock<IServerResourceMonitor> _monitorMock;
        private readonly GameServerFilesService _service;

        public GameServerFilesServiceTests()
        {
            _repoMock = new Mock<IGameServerRepository>();
            _discoveryMock = new Mock<INodeAgentDiscovery>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _loggerMock = new Mock<ILogger<GameServerFilesService>>();
            _monitorMock = new Mock<IServerResourceMonitor>();

            _service = new GameServerFilesService(
                _repoMock.Object,
                _discoveryMock.Object,
                _httpClientFactoryMock.Object,
                _loggerMock.Object,
                _monitorMock.Object);
        }

        [Fact]
        public async Task ListFilesAsync_WhenNoActiveContainer_ReturnsEmptyList()
        {
            _monitorMock
                .Setup(m => m.GetSnapshotAsync("s1", It.IsAny<CancellationToken>()))
                .ReturnsAsync((ServerResourceUsage?)null);
            _discoveryMock
                .Setup(d => d.GetAgentForServerAsync("s1"))
                .ReturnsAsync((NodeAgentEndpoint?)null);

            var files = await _service.ListFilesAsync("s1", "/data");

            Assert.Empty(files);
        }

        [Fact]
        public async Task ListFilesAsync_WhenAgentResponds_ReturnsFileList()
        {
            var agent = new NodeAgentEndpoint { NodeId = "n1", InternalUrl = "http://agent1:8080" };
            _monitorMock
                .Setup(m => m.GetSnapshotAsync("s1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServerResourceUsage
                {
                    ServerId = "s1",
                    ContainerIds = new List<string> { "c1" }
                });
            _discoveryMock
                .Setup(d => d.GetAgentForContainerAsync("c1"))
                .ReturnsAsync(agent);

            var fileList = new List<FileItemDto>
            {
                new() { Name = "config.json", Path = "/data/config.json", IsDirectory = false, Size = 100 }
            };

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/containers/c1/files")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(fileList))
                });

            var httpClient = new HttpClient(handlerMock.Object);
            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var result = await _service.ListFilesAsync("s1", "/data");

            Assert.Single(result);
            Assert.Equal("config.json", result[0].Name);
        }

        [Fact]
        public async Task GetFileContentTextAsync_WhenServerNotRunning_ThrowsInvalidOperationException()
        {
            _monitorMock
                .Setup(m => m.GetSnapshotAsync("s1", It.IsAny<CancellationToken>()))
                .ReturnsAsync((ServerResourceUsage?)null);
            _discoveryMock
                .Setup(d => d.GetAgentForServerAsync("s1"))
                .ReturnsAsync((NodeAgentEndpoint?)null);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.GetFileContentTextAsync("s1", "/data", "test.txt"));
        }

        [Fact]
        public async Task GetFileContentTextAsync_WhenFileNotFound_ThrowsFileNotFoundException()
        {
            var agent = new NodeAgentEndpoint { NodeId = "n1", InternalUrl = "http://agent1:8080" };
            _monitorMock
                .Setup(m => m.GetSnapshotAsync("s1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServerResourceUsage { ContainerIds = new List<string> { "c1" } });
            _discoveryMock
                .Setup(d => d.GetAgentForContainerAsync("c1"))
                .ReturnsAsync(agent);

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

            var httpClient = new HttpClient(handlerMock.Object);
            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                _service.GetFileContentTextAsync("s1", "/data", "missing.txt"));
        }

        [Fact]
        public async Task SaveFileContentTextAsync_WhenSuccessful_Completes()
        {
            var agent = new NodeAgentEndpoint { NodeId = "n1", InternalUrl = "http://agent1:8080" };
            _monitorMock
                .Setup(m => m.GetSnapshotAsync("s1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServerResourceUsage { ContainerIds = new List<string> { "c1" } });
            _discoveryMock
                .Setup(d => d.GetAgentForContainerAsync("c1"))
                .ReturnsAsync(agent);

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            var httpClient = new HttpClient(handlerMock.Object);
            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            await _service.SaveFileContentTextAsync("s1", "/data", "test.txt", "hello world");
        }

        [Fact]
        public async Task CreateDirectoryAsync_WhenSuccessful_Completes()
        {
            var agent = new NodeAgentEndpoint { NodeId = "n1", InternalUrl = "http://agent1:8080" };
            _monitorMock
                .Setup(m => m.GetSnapshotAsync("s1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServerResourceUsage { ContainerIds = new List<string> { "c1" } });
            _discoveryMock
                .Setup(d => d.GetAgentForContainerAsync("c1"))
                .ReturnsAsync(agent);

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            var httpClient = new HttpClient(handlerMock.Object);
            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            await _service.CreateDirectoryAsync("s1", "/data", "new_folder");
        }

        [Fact]
        public async Task DeleteFileOrDirectoryAsync_WhenSuccessful_Completes()
        {
            var agent = new NodeAgentEndpoint { NodeId = "n1", InternalUrl = "http://agent1:8080" };
            _monitorMock
                .Setup(m => m.GetSnapshotAsync("s1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServerResourceUsage { ContainerIds = new List<string> { "c1" } });
            _discoveryMock
                .Setup(d => d.GetAgentForContainerAsync("c1"))
                .ReturnsAsync(agent);

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            var httpClient = new HttpClient(handlerMock.Object);
            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            await _service.DeleteFileOrDirectoryAsync("s1", "/data", "old_file.txt", recursive: false);
        }
    }
}
