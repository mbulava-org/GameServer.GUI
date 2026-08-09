using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using GameServer.Web.Configurations;
using GameServer.Web.Models.V2;
using GameServer.Web.Services.V2;
using Moq;

namespace GameServer.Web.Tests.Services.V2;

public class GameServerV2ApiServiceTests
{
    [Fact]
    public async Task GetListAsync_WhenApiReturnsPayload_ShouldDeserializeServers()
    {
        // Arrange
        var service = CreateService(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/api/v2/gameservers")
            {
                return CreateJsonResponse(new List<GameServerListItem>
                {
                    new()
                    {
                        ServerId = "srv-1",
                        Name = "Minecraft Survival",
                        Status = "Running"
                    }
                });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        // Act
        var result = await service.GetListAsync();

        // Assert
        var server = Assert.Single(result);
        Assert.Equal("srv-1", server.ServerId);
        Assert.Equal("Running", server.Status);
    }

    [Fact]
    public async Task GetByServerIdAsync_WhenApiReturnsNotFound_ShouldReturnNull()
    {
        // Arrange
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        // Act
        var result = await service.GetByServerIdAsync("missing");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateAsync_WhenApiReturnsPayload_ShouldDeserializeValidationResult()
    {
        // Arrange
        var service = CreateService(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/api/v2/gameservers/validate")
            {
                return CreateJsonResponse(new GameServerValidationResult
                {
                    IsValid = true,
                    ResolvedPorts = [ new GameServerResolvedPort { ContainerPort = 25565, Protocol = "tcp" } ]
                });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        // Act
        var result = await service.ValidateAsync(new SaveGameServerRequest
        {
            Name = "Minecraft Survival",
            GameTypeRevisionId = 10
        });

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(25565, Assert.Single(result.ResolvedPorts).ContainerPort);
    }

    [Fact]
    public async Task CreateAsync_WhenApiReturnsPayload_ShouldDeserializeDetail()
    {
        // Arrange
        var service = CreateService(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/api/v2/gameservers")
            {
                return CreateJsonResponse(new GameServerDetail
                {
                    ServerId = "srv-1",
                    Name = "Minecraft Survival",
                    ServiceName = "gameserver-srv-1",
                    Status = "Stopped"
                });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        // Act
        var result = await service.CreateAsync(new SaveGameServerRequest
        {
            Name = "Minecraft Survival",
            GameTypeRevisionId = 10
        });

        // Assert
        Assert.Equal("srv-1", result.ServerId);
        Assert.Equal("Stopped", result.Status);
    }

    private static GameServerV2ApiService CreateService(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new StubHttpMessageHandler(handler))
            {
                BaseAddress = new Uri("http://localhost/")
            });

        var options = new GameServerDockerApi
        {
            BaseUri = "http://localhost/"
        };

        return new GameServerV2ApiService(httpClientFactory.Object, options);
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
