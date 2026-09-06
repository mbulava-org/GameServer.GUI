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

    [Fact]
    public async Task DeleteAsync_WhenApiReturnsNoContent_ShouldSucceed()
    {
        // Arrange
        var requestUriCaptured = string.Empty;
        var service = CreateService(request =>
        {
            if (request.Method == HttpMethod.Delete && request.RequestUri?.AbsolutePath == "/api/v2/gameservers/srv-1")
            {
                requestUriCaptured = request.RequestUri.ToString();
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        // Act
        await service.DeleteAsync("srv-1", softDelete: true);

        // Assert
        Assert.Contains("/api/v2/gameservers/srv-1?softDelete=True", requestUriCaptured);
    }

    [Fact]
    public async Task StartAsync_WhenApiReturnsPayload_ShouldDeserializeDetail()
    {
        var service = CreateService(request =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/api/v2/gameservers/srv-1/start")
            {
                return CreateJsonResponse(new GameServerDetail
                {
                    ServerId = "srv-1",
                    Name = "Minecraft Survival",
                    Status = "Running"
                });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var result = await service.StartAsync("srv-1");
        Assert.Equal("srv-1", result.ServerId);
        Assert.Equal("Running", result.Status);
    }

    [Fact]
    public async Task StopAsync_WhenApiReturnsPayload_ShouldDeserializeDetail()
    {
        var service = CreateService(request =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/api/v2/gameservers/srv-1/stop")
            {
                return CreateJsonResponse(new GameServerDetail
                {
                    ServerId = "srv-1",
                    Name = "Minecraft Survival",
                    Status = "Stopped"
                });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var result = await service.StopAsync("srv-1");
        Assert.Equal("srv-1", result.ServerId);
        Assert.Equal("Stopped", result.Status);
    }

    [Fact]
    public async Task RestartAsync_WhenApiReturnsPayload_ShouldDeserializeDetail()
    {
        var service = CreateService(request =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/api/v2/gameservers/srv-1/restart")
            {
                return CreateJsonResponse(new GameServerDetail
                {
                    ServerId = "srv-1",
                    Name = "Minecraft Survival",
                    Status = "Running"
                });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var result = await service.RestartAsync("srv-1");
        Assert.Equal("srv-1", result.ServerId);
        Assert.Equal("Running", result.Status);
    }

    [Fact]
    public async Task RedeployAsync_WhenApiReturnsPayload_ShouldDeserializeDetail()
    {
        var service = CreateService(request =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/api/v2/gameservers/srv-1/redeploy")
            {
                return CreateJsonResponse(new GameServerDetail
                {
                    ServerId = "srv-1",
                    Name = "Minecraft Survival",
                    Status = "Running"
                });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var result = await service.RedeployAsync("srv-1");
        Assert.Equal("srv-1", result.ServerId);
        Assert.Equal("Running", result.Status);
    }

    [Fact]
    public async Task UpdateAsync_WhenApiReturnsPayload_ShouldDeserializeDetail()
    {
        var service = CreateService(request =>
        {
            if (request.Method == HttpMethod.Put && request.RequestUri?.AbsolutePath == "/api/v2/gameservers/srv-1")
            {
                return CreateJsonResponse(new GameServerDetail
                {
                    ServerId = "srv-1",
                    Name = "Updated Server",
                    Status = "Running"
                });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var result = await service.UpdateAsync("srv-1", new SaveGameServerRequest
        {
            Name = "Updated Server",
            GameTypeRevisionId = 10
        });

        Assert.Equal("srv-1", result.ServerId);
        Assert.Equal("Updated Server", result.Name);
    }

    [Fact]
    public async Task PreviewAsync_WhenApiReturnsPayload_ShouldDeserializePreview()
    {
        var service = CreateService(request =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/api/v2/gameservers/preview")
            {
                return CreateJsonResponse(new GameServerDeploymentPreview
                {
                    ServiceName = "gameserver-srv-1",
                    ImageReference = "itzg/minecraft-server",
                    VersionTag = "latest",
                    Notices = ["Notice 1"]
                });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var result = await service.PreviewAsync(new SaveGameServerRequest
        {
            Name = "Minecraft Server",
            GameTypeRevisionId = 10
        });

        Assert.Equal("gameserver-srv-1", result.ServiceName);
        Assert.Single(result.Notices);
    }

    [Fact]
    public async Task CheckPortAvailabilityAsync_WhenApiReturnsPayload_ShouldDeserializeAvailabilityResult()
    {
        var service = CreateService(request =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/api/v2/gameservers/ports/availability")
            {
                return CreateJsonResponse(new GameServerPortAvailabilityResult
                {
                    Ports =
                    [
                        new GameServerPortAvailability { Port = 25565, Protocol = "tcp", IsAvailable = true }
                    ]
                });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var result = await service.CheckPortAvailabilityAsync(new GameServerPortAvailabilityRequest
        {
            Ports = [new GameServerPortAvailabilityRequestPort { Port = 25565, Protocol = "tcp" }]
        });

        var port = Assert.Single(result.Ports);
        Assert.Equal(25565, port.Port);
        Assert.True(port.IsAvailable);
    }

    [Fact]
    public async Task GetResourceHistoryAsync_WithDatesAndLimit_ShouldPassQueryParamsAndDeserializeList()
    {
        var requestedUri = string.Empty;
        var fromDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var toDate = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        var service = CreateService(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/api/v2/gameservers/srv-1/resources/history")
            {
                requestedUri = request.RequestUri.ToString();
                return CreateJsonResponse(new List<GameServerResourceHistoryItem>
                {
                    new()
                    {
                        Timestamp = fromDate,
                        CpuUsagePercent = 12.5,
                        MemoryUsageBytes = 1024 * 1024 * 500,
                        MemoryLimitBytes = 1024 * 1024 * 1024
                    }
                });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var result = await service.GetResourceHistoryAsync("srv-1", from: fromDate, to: toDate, limit: 100);

        Assert.Contains("limit=100", requestedUri);
        Assert.Contains("from=", requestedUri);
        Assert.Contains("to=", requestedUri);
        var item = Assert.Single(result);
        Assert.Equal(12.5, item.CpuUsagePercent);
    }

    [Fact]
    public async Task NullOrWhitespaceServerId_ShouldThrowArgumentException()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK));

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetByServerIdAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync("", new SaveGameServerRequest()));
        await Assert.ThrowsAsync<ArgumentException>(() => service.StartAsync("   "));
        await Assert.ThrowsAsync<ArgumentException>(() => service.StopAsync("   "));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RestartAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RedeployAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => service.DeleteAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetResourceHistoryAsync(""));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.StopAsync(null!));
    }

    [Fact]
    public async Task NullRequestArguments_ShouldThrowArgumentNullException()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK));

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.ValidateAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.PreviewAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.CheckPortAvailabilityAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.CreateAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.UpdateAsync("srv-1", null!));
    }

    [Fact]
    public async Task MissingBaseUri_ShouldThrowInvalidOperationException()
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());

        var service = new GameServerV2ApiService(httpClientFactory.Object, new GameServerDockerApi { BaseUri = "" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetListAsync());
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
