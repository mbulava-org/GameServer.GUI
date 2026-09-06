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

        [Fact]
        public async Task GetFileStreamAsync_WhenSuccessful_ReturnsStreamAndMetadata()
        {
            var agent = new NodeAgentEndpoint { NodeId = "n1", InternalUrl = "http://agent1:8080" };
            _monitorMock
                .Setup(m => m.GetSnapshotAsync("s1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServerResourceUsage { ContainerIds = new List<string> { "c1" } });
            _discoveryMock
                .Setup(d => d.GetAgentForContainerAsync("c1"))
                .ReturnsAsync(agent);

            var handlerMock = new Mock<HttpMessageHandler>();
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("file content stream")
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
            response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
            {
                FileName = "\"downloaded.txt\""
            };

            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            var httpClient = new HttpClient(handlerMock.Object);
            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var (stream, contentType, fileName) = await _service.GetFileStreamAsync("s1", "/data", "downloaded.txt");

            Assert.NotNull(stream);
            Assert.Equal("text/plain", contentType);
            Assert.Equal("downloaded.txt", fileName);
        }

        [Fact]
        public async Task GetFileStreamAsync_WhenServerNotRunning_ThrowsInvalidOperationException()
        {
            _monitorMock
                .Setup(m => m.GetSnapshotAsync("s1", It.IsAny<CancellationToken>()))
                .ReturnsAsync((ServerResourceUsage?)null);
            _discoveryMock
                .Setup(d => d.GetAgentForServerAsync("s1"))
                .ReturnsAsync((NodeAgentEndpoint?)null);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.GetFileStreamAsync("s1", "/data", "test.txt"));
        }

        [Fact]
        public async Task GetFileStreamAsync_WhenNotFound_ThrowsFileNotFoundException()
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
                _service.GetFileStreamAsync("s1", "/data", "missing.txt"));
        }

        [Fact]
        public async Task GetFileStreamAsync_WhenServerError_ThrowsHttpRequestException()
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
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var httpClient = new HttpClient(handlerMock.Object);
            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                _service.GetFileStreamAsync("s1", "/data", "error.txt"));
        }

        [Fact]
        public async Task UploadFileAsync_WhenSuccessful_Completes()
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

            using var mem = new MemoryStream(new byte[] { 1, 2, 3 });
            await _service.UploadFileAsync("s1", @"\data\saves", "sub", mem, "world.sav");
        }

        [Fact]
        public async Task UploadFileAsync_WhenValidationFails_ThrowsExceptions()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _service.UploadFileAsync("s1", "/data", null, null!, "test.txt"));

            using var mem = new MemoryStream();
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.UploadFileAsync("s1", "/data", null, mem, ""));
        }

        [Fact]
        public async Task UploadFileAsync_WhenServerNotRunning_ThrowsInvalidOperationException()
        {
            _monitorMock
                .Setup(m => m.GetSnapshotAsync("s1", It.IsAny<CancellationToken>()))
                .ReturnsAsync((ServerResourceUsage?)null);
            _discoveryMock
                .Setup(d => d.GetAgentForServerAsync("s1"))
                .ReturnsAsync((NodeAgentEndpoint?)null);

            using var mem = new MemoryStream();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.UploadFileAsync("s1", "/data", null, mem, "file.txt"));
        }

        [Fact]
        public async Task ResolveAgent_WhenMonitorThrowsException_FallsBackToDiscovery()
        {
            _monitorMock
                .Setup(m => m.GetSnapshotAsync("s1", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Monitor failure"));

            _discoveryMock
                .Setup(d => d.GetAgentForServerAsync("s1"))
                .ReturnsAsync(new NodeAgentEndpoint { InternalUrl = "http://fallback:8080" });

            var files = await _service.ListFilesAsync("s1", "/data");
            Assert.Empty(files);
        }
    }
}
