using System.Net;
using System.Text;
using System.Text.Json;
using GameServer.Web.Configurations;
using GameServer.Web.Models.V2;
using GameServer.Web.Services.V2;
using Microsoft.Extensions.Options;
using Moq;

namespace GameServer.Web.Tests.Services.V2;

public class GameServerFilesApiServiceTests
{
    [Fact]
    public async Task ListFilesAsync_WhenCalled_ReturnsFileItems()
    {
        var items = new List<FileItem>
        {
            new() { Name = "server.properties", Path = "/server.properties", Size = 512, IsDirectory = false }
        };

        var service = CreateService(req =>
        {
            Assert.Contains("/api/v2/gameservers/srv-1/files", req.RequestUri?.AbsolutePath);
            return CreateJsonResponse(items);
        });

        var result = await service.ListFilesAsync("srv-1", "/data", "subfolder");
        var item = Assert.Single(result);
        Assert.Equal("server.properties", item.Name);
    }

    [Fact]
    public async Task GetContentAsync_WhenCalled_ReturnsStringContent()
    {
        var service = CreateService(req =>
        {
            Assert.Contains("/api/v2/gameservers/srv-1/files/content", req.RequestUri?.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("motd=Hello World", Encoding.UTF8, "text/plain")
            };
        });

        var content = await service.GetContentAsync("srv-1", "/data", "server.properties");
        Assert.Equal("motd=Hello World", content);
    }

    [Fact]
    public async Task SaveContentAsync_WhenCalled_SendsPutRequest()
    {
        bool wasCalled = false;
        var service = CreateService(req =>
        {
            Assert.Equal(HttpMethod.Put, req.Method);
            Assert.Contains("/api/v2/gameservers/srv-1/files/content", req.RequestUri?.AbsolutePath);
            wasCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await service.SaveContentAsync("srv-1", "/data", "server.properties", "motd=Updated");
        Assert.True(wasCalled);
    }

    [Fact]
    public async Task DownloadAsync_WhenCalled_ReturnsByteArray()
    {
        var bytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var service = CreateService(req =>
        {
            Assert.Contains("/api/v2/gameservers/srv-1/files/download", req.RequestUri?.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            };
        });

        var result = await service.DownloadAsync("srv-1", "/data", "world.zip");
        Assert.Equal(bytes, result);
    }

    [Fact]
    public async Task UploadAsync_WhenCalled_SendsMultipartFormData()
    {
        bool wasCalled = false;
        var service = CreateService(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Contains("/api/v2/gameservers/srv-1/files/upload", req.RequestUri?.AbsolutePath);
            wasCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test data"));
        await service.UploadAsync("srv-1", "/data", null, "test.txt", stream);
        Assert.True(wasCalled);
    }

    [Fact]
    public async Task CreateDirectoryAsync_WhenCalled_SendsPostRequest()
    {
        bool wasCalled = false;
        var service = CreateService(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Contains("/api/v2/gameservers/srv-1/files/directory", req.RequestUri?.AbsolutePath);
            wasCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await service.CreateDirectoryAsync("srv-1", "/data", "newfolder");
        Assert.True(wasCalled);
    }

    [Fact]
    public async Task DeleteAsync_WhenCalled_SendsDeleteRequest()
    {
        bool wasCalled = false;
        var service = CreateService(req =>
        {
            Assert.Equal(HttpMethod.Delete, req.Method);
            Assert.Contains("/api/v2/gameservers/srv-1/files", req.RequestUri?.AbsolutePath);
            Assert.Contains("recursive=True", req.RequestUri?.Query);
            wasCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await service.DeleteAsync("srv-1", "/data", "oldfolder", recursive: true);
        Assert.True(wasCalled);
    }

    private static GameServerFilesApiService CreateService(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new StubHttpMessageHandler(handler))
            {
                BaseAddress = new Uri("http://localhost:5164/")
            });

        var options = Options.Create(new GameServerDockerApi
        {
            BaseUri = "http://localhost:5164/"
        });

        return new GameServerFilesApiService(httpClientFactory.Object, options);
    }

    private static HttpResponseMessage CreateJsonResponse<T>(T payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
