using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using GameServer.Web.Components.Pages.GameTypes;
using GameServer.Web.Configurations;
using GameServer.Web.Models.V2;
using GameServer.Web.Services.V2;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Radzen;

namespace GameServer.Web.Tests.Components.GameTypes.V2;

public sealed class GameTypeManagerV2Tests : BunitContext
{
    public GameTypeManagerV2Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void GameTypeManagerV2_ShouldRenderDeleteButtonForEachRow()
    {
        var gameTypes = new List<GameTypeListItem>
        {
            new() { Id = 1, Key = "minecraft", DisplayName = "Minecraft", Type = "docker", IsActive = true, RevisionCount = 1, PublishedRevisionCount = 1, UpdatedAt = DateTime.UtcNow },
            new() { Id = 2, Key = "valheim", DisplayName = "Valheim", Type = "docker", IsActive = true, RevisionCount = 1, PublishedRevisionCount = 1, UpdatedAt = DateTime.UtcNow }
        };

        RegisterApi((request, deletedKeys) => request.Method == HttpMethod.Get
            ? CreateJsonResponse(gameTypes.Where(gameType => !deletedKeys.Contains(gameType.Key)).ToList())
            : new HttpResponseMessage(HttpStatusCode.NotFound));

        var cut = Render<GameTypeManagerV2>();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("button[title='Delete']").Count));
    }

    [Fact]
    public void GameTypeManagerV2_DeleteButton_ShouldRemoveDeletedRow()
    {
        var gameTypes = new List<GameTypeListItem>
        {
            new() { Id = 1, Key = "minecraft", DisplayName = "Minecraft", Type = "docker", IsActive = true, RevisionCount = 1, PublishedRevisionCount = 1, UpdatedAt = DateTime.UtcNow },
            new() { Id = 2, Key = "valheim", DisplayName = "Valheim", Type = "docker", IsActive = true, RevisionCount = 1, PublishedRevisionCount = 1, UpdatedAt = DateTime.UtcNow }
        };
        var getRequestCount = 0;
        var deleteRequestCount = 0;

        RegisterApi((request, deletedKeys) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                getRequestCount++;
                return CreateJsonResponse(gameTypes.Where(gameType => !deletedKeys.Contains(gameType.Key)).ToList());
            }

            if (request.Method == HttpMethod.Delete && request.RequestUri?.AbsolutePath == "/api/v2/gametypes/minecraft")
            {
                deleteRequestCount++;
                deletedKeys.Add("minecraft");
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var cut = Render<GameTypeManagerV2>();
        cut.WaitForAssertion(() => Assert.Contains("Minecraft", cut.Markup));

        cut.FindAll("button[title='Delete']").First().Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, deleteRequestCount);
            Assert.True(getRequestCount >= 2);
            Assert.Contains("Valheim", cut.Markup);
            Assert.DoesNotContain("/api/v2/gametypes/minecraft", cut.Markup);
        });
    }

    private void RegisterApi(Func<HttpRequestMessage, HashSet<string>, HttpResponseMessage> responder)
    {
        var deletedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Services.AddSingleton<NotificationService>();
        Services.AddSingleton(CreateApiService(request => responder(request, deletedKeys)));
    }

    private GameTypeV2ApiService CreateApiService(Func<HttpRequestMessage, HttpResponseMessage> responder)
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

    private static HttpResponseMessage CreateJsonResponse<T>(T payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
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
