using System.Net;
using System.Text;
using System.Text.Json;
using GameServer.Web.Configurations;
using GameServer.Web.Models.V2;
using GameServer.Web.Services.V2;
using Moq;

namespace GameServer.Web.Tests.Services.V2;

public class MountTypeConfigApiServiceTests
{
    [Fact]
    public async Task GetAllAsync_WhenApiReturnsList_ShouldDeserializeConfigs()
    {
        // Arrange
        var expected = new List<MountTypeConfig>
        {
            new()
            {
                Key = "nfs",
                DisplayName = "NFS Mount",
                Description = "Remote NFS export",
                VolumeNameFormat = "{gameTypeKey}-{serverId}-{Source}",
                Options = new Dictionary<string, string> { ["Driver"] = "local" }
            }
        };

        var service = CreateService(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri?.PathAndQuery.Contains("api/v2/mounttypeconfigs") == true)
            {
                return CreateJsonResponse(expected);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        // Act
        var result = await service.GetAllAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Equal("nfs", result[0].Key);
        Assert.Equal("NFS Mount", result[0].DisplayName);
        Assert.Equal("local", result[0].Options?["Driver"]);
    }

    [Fact]
    public async Task GetAsync_WhenKeyExists_ShouldReturnConfig()
    {
        // Arrange
        var expected = new MountTypeConfig
        {
            Key = "volume",
            DisplayName = "Docker Named Volume",
            VolumeNameFormat = "{gameTypeKey}-{serverId}-{Source}"
        };

        var service = CreateService(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri?.PathAndQuery.Contains("api/v2/mounttypeconfigs/volume") == true)
            {
                return CreateJsonResponse(expected);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        // Act
        var result = await service.GetAsync("volume", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("volume", result.Key);
        Assert.Equal("Docker Named Volume", result.DisplayName);
    }

    [Fact]
    public async Task SaveAsync_WhenValidConfig_ShouldPutAndReturnSavedConfig()
    {
        // Arrange
        var configToSave = new MountTypeConfig
        {
            Key = "nfs",
            DisplayName = "NFS Mount Updated",
            VolumeNameFormat = "{gameTypeKey}-{serverId}-{Source}"
        };

        var service = CreateService(request =>
        {
            if (request.Method == HttpMethod.Put && request.RequestUri?.PathAndQuery.Contains("api/v2/mounttypeconfigs/nfs") == true)
            {
                return CreateJsonResponse(configToSave);
            }

            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        });

        // Act
        var result = await service.SaveAsync(configToSave, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("NFS Mount Updated", result.DisplayName);
    }

    [Fact]
    public async Task DeleteAsync_WhenKeyExists_ShouldSendDelete()
    {
        // Arrange
        var deleteCalled = false;
        var service = CreateService(request =>
        {
            if (request.Method == HttpMethod.Delete && request.RequestUri?.PathAndQuery.Contains("api/v2/mounttypeconfigs/custom") == true)
            {
                deleteCalled = true;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        // Act
        await service.DeleteAsync("custom", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(deleteCalled);
    }

    [Fact]
    public async Task SaveAsync_WhenNull_ShouldThrowArgumentNullException()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.SaveAsync(null!, TestContext.Current.CancellationToken));
    }

    private static MountTypeConfigApiService CreateService(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(new StubHttpMessageHandler(handler))
            {
                BaseAddress = new Uri("http://localhost/")
            });

        return new MountTypeConfigApiService(httpClientFactory.Object, new GameServerDockerApi { BaseUri = "http://localhost/" });
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
