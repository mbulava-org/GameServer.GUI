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
