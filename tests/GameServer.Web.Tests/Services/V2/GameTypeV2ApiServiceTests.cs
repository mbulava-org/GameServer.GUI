using System.Net;
using System.Text;
using System.Text.Json;
using GameServer.Web.Configurations;
using GameServer.Web.Models.V2;
using GameServer.Web.Services.V2;
using Moq;

namespace GameServer.Web.Tests.Services.V2;

public sealed class GameTypeV2ApiServiceTests
{
    [Fact]
    public async Task ExportAsync_WhenApiReturnsPayload_ShouldDeserializePortablePackage()
    {
        var service = CreateService(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.EndsWith("/api/v2/gametypes/minecraft/export", request.RequestUri!.AbsoluteUri, StringComparison.OrdinalIgnoreCase);

            return CreateJsonResponse(new PortableGameTypePackage
            {
                GameType = new PortableGameType
                {
                    Key = "minecraft",
                    DisplayName = "Minecraft",
                    Type = "docker",
                    Revisions = [ new PortableGameTypeRevision { VersionTag = "latest", ImageReference = "itzg/minecraft-server" } ]
                }
            });
        });

        var result = await service.ExportAsync("minecraft");

        Assert.Equal("minecraft", result.GameType.Key);
        Assert.Single(result.GameType.Revisions);
    }

    [Fact]
    public async Task ImportAsync_WhenApiReturnsPayload_ShouldDeserializeGameTypeDetail()
    {
        var service = CreateService(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.EndsWith("/api/v2/gametypes/import", request.RequestUri!.AbsoluteUri, StringComparison.OrdinalIgnoreCase);

            return CreateJsonResponse(new GameTypeDetail
            {
                Key = "minecraft",
                DisplayName = "Minecraft",
                Type = "docker",
                Revisions = [ new GameTypeRevision { Id = 10, VersionTag = "latest", ImageReference = "itzg/minecraft-server" } ]
            }, HttpStatusCode.Created);
        });

        var result = await service.ImportAsync(new PortableGameTypePackage
        {
            GameType = new PortableGameType
            {
                Key = "minecraft",
                DisplayName = "Minecraft",
                Type = "docker",
                Revisions = [ new PortableGameTypeRevision { VersionTag = "latest", ImageReference = "itzg/minecraft-server" } ]
            }
        });

        Assert.Equal("minecraft", result.Key);
        Assert.Single(result.Revisions);
    }

    [Fact]
    public async Task DetectSetupAsync_WhenGameTypeIsUnsaved_ShouldCallImageOnlyDetectionEndpoint()
    {
        var service = CreateService(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.EndsWith("/api/v2/gametypes/detection/scan-tag", request.RequestUri!.AbsoluteUri, StringComparison.OrdinalIgnoreCase);

            return CreateJsonResponse(new GameTypeSetupDetectionResult
            {
                ImageReference = "itzg/minecraft-server",
                VersionTag = "latest",
                Ports = [ new DetectedPort { ContainerPort = 25565, Protocol = "tcp" } ]
            });
        });

        var result = await service.DetectSetupAsync("itzg/minecraft-server", "latest");

        Assert.Equal("itzg/minecraft-server", result.ImageReference);
        Assert.Single(result.Ports);
    }

    [Fact]
    public async Task GetListAsync_WhenApiReturnsList_ShouldReturnGameTypes()
    {
        var expected = new List<GameTypeListItem>
        {
            new() { Id = 1, Key = "minecraft", DisplayName = "Minecraft" }
        };

        var service = CreateService(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Contains("api/v2/gametypes?includeInactive=true", request.RequestUri!.PathAndQuery, StringComparison.OrdinalIgnoreCase);
            return CreateJsonResponse(expected);
        });

        var result = await service.GetListAsync(true, TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal("minecraft", result[0].Key);
    }

    [Fact]
    public async Task GetByKeyAsync_WhenExists_ShouldReturnGameTypeDetail()
    {
        var expected = new GameTypeDetail { Key = "valheim", DisplayName = "Valheim" };
        var service = CreateService(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Contains("api/v2/gametypes/valheim", request.RequestUri!.PathAndQuery);
            return CreateJsonResponse(expected);
        });

        var result = await service.GetByKeyAsync("valheim", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("valheim", result.Key);
    }

    [Fact]
    public async Task GetByKeyAsync_WhenNotFound_ShouldReturnNull()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var result = await service.GetByKeyAsync("non-existent", TestContext.Current.CancellationToken);
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_WhenValid_ShouldPostAndReturnDetail()
    {
        var requestPayload = new SaveGameTypeRequest { Key = "factorio", DisplayName = "Factorio" };
        var expected = new GameTypeDetail { Key = "factorio", DisplayName = "Factorio" };

        var service = CreateService(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            return CreateJsonResponse(expected, HttpStatusCode.Created);
        });

        var result = await service.CreateAsync(requestPayload, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("factorio", result.Key);
    }

    [Fact]
    public async Task UpdateAsync_WhenValid_ShouldPutAndReturnDetail()
    {
        var requestPayload = new SaveGameTypeRequest { Key = "factorio", DisplayName = "Factorio Updated" };
        var expected = new GameTypeDetail { Key = "factorio", DisplayName = "Factorio Updated" };

        var service = CreateService(request =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            return CreateJsonResponse(expected);
        });

        var result = await service.UpdateAsync("factorio", requestPayload, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("Factorio Updated", result.DisplayName);
    }

    [Fact]
    public async Task DeleteAsync_WhenValid_ShouldSendDelete()
    {
        var deleteCalled = false;
        var service = CreateService(request =>
        {
            if (request.Method == HttpMethod.Delete && request.RequestUri?.AbsolutePath == "/api/v2/gametypes/custom")
            {
                deleteCalled = true;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        await service.DeleteAsync("custom", TestContext.Current.CancellationToken);
        Assert.True(deleteCalled);
    }

    [Fact]
    public async Task AddRevisionAsync_WhenValid_ShouldPostAndReturnRevision()
    {
        var revisionRequest = new SaveGameTypeRevisionRequest { VersionTag = "1.0.0", ImageReference = "custom/img" };
        var expected = new GameTypeRevision { Id = 1, VersionTag = "1.0.0", ImageReference = "custom/img" };

        var service = CreateService(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Contains("api/v2/gametypes/custom/revisions", request.RequestUri!.AbsolutePath);
            return CreateJsonResponse(expected, HttpStatusCode.Created);
        });

        var result = await service.AddRevisionAsync("custom", revisionRequest, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("1.0.0", result.VersionTag);
    }

    [Fact]
    public async Task UpdateRevisionAsync_WhenValid_ShouldPutAndReturnRevision()
    {
        var revisionRequest = new SaveGameTypeRevisionRequest { VersionTag = "1.0.1", ImageReference = "custom/img" };
        var expected = new GameTypeRevision { Id = 1, VersionTag = "1.0.1", ImageReference = "custom/img" };

        var service = CreateService(request =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Contains("api/v2/gametypes/custom/revisions/1", request.RequestUri!.AbsolutePath);
            return CreateJsonResponse(expected);
        });

        var result = await service.UpdateRevisionAsync("custom", 1, revisionRequest, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("1.0.1", result.VersionTag);
    }

    [Fact]
    public async Task PublishRevisionAsync_WhenValid_ShouldPostAndReturnRevision()
    {
        var expected = new GameTypeRevision { Id = 1, VersionTag = "1.0.0", IsPublished = true };

        var service = CreateService(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Contains("api/v2/gametypes/custom/revisions/1/publish", request.RequestUri!.AbsolutePath);
            return CreateJsonResponse(expected);
        });

        var result = await service.PublishRevisionAsync("custom", 1, true, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.True(result.IsPublished);
    }

    [Fact]
    public async Task SetCurrentRevisionAsync_WhenValid_ShouldPost()
    {
        var setCurrentCalled = false;
        var service = CreateService(request =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/api/v2/gametypes/custom/revisions/1/set-current")
            {
                setCurrentCalled = true;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        await service.SetCurrentRevisionAsync("custom", 1, TestContext.Current.CancellationToken);
        Assert.True(setCurrentCalled);
    }

    [Fact]
    public async Task CompareSetupAsync_WhenValid_ShouldReturnComparison()
    {
        var expected = new GameTypeSetupComparisonResult
        {
            RevisionId = 1,
            HasChanges = true,
            AddedPorts = ["8080/tcp"]
        };

        var service = CreateService(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Contains("api/v2/gametypes/custom/detection/compare", request.RequestUri!.AbsolutePath);
            return CreateJsonResponse(expected);
        });

        var result = await service.CompareSetupAsync("custom", "custom/image", "1.0.0", 1, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.True(result.HasChanges);
        Assert.Single(result.AddedPorts);
    }

    private static GameTypeV2ApiService CreateService(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new StubHttpMessageHandler(responder);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost/")
            });

        var options = new GameServerDockerApi
        {
            BaseUri = "http://localhost/"
        };

        return new GameTypeV2ApiService(httpClientFactory.Object, options);
    }

    private static HttpResponseMessage CreateJsonResponse<T>(T payload, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
    }
}
