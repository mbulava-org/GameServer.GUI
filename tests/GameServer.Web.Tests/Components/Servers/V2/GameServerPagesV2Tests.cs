using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using GameServer.Web.Components.Pages.Servers;
using GameServer.Web.Configurations;
using GameServer.Web.Models.V2;
using GameServer.Web.Services.V2;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Radzen;

namespace GameServer.Web.Tests.Components.Servers.V2;

public sealed class GameServerPagesV2Tests : BunitContext
{
    public GameServerPagesV2Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void GameServerManagerV2_ShouldRenderServerList()
    {
        // Arrange
        RegisterApis(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/api/v2/gameservers")
            {
                return CreateJsonResponse(new List<GameServerListItem>
                {
                    new()
                    {
                        ServerId = "srv-1",
                        Name = "Minecraft Survival",
                        Status = "Running",
                        GameTypeDisplayName = "Minecraft",
                        RevisionVersionTag = "1.21.2",
                        ResolvedPorts = [ new GameServerResolvedPort { ContainerPort = 25565, Protocol = "tcp", DisplayOrder = 0 } ]
                    }
                });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        // Act
        var cut = Render<GameServerManagerV2>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Game Servers V2", cut.Markup);
            Assert.Contains("Minecraft Survival", cut.Markup);
            Assert.Contains("25565/tcp", cut.Markup);
        });
    }

    [Fact]
    public void GameServerDetailsV2_ShouldRenderValidationPreview()
    {
        // Arrange
        RegisterApis(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/api/v2/gameservers/srv-1")
            {
                return CreateJsonResponse(new GameServerDetail
                {
                    ServerId = "srv-1",
                    Name = "Minecraft Survival",
                    GameTypeRevisionId = 10,
                    ServiceName = "gameserver-srv-1",
                    Status = "Stopped",
                    GameTypeDisplayName = "Minecraft",
                    Settings = [ new GameServerSetting { SettingKey = "SERVER_PORT", Value = "25565" } ]
                });
            }

            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/api/v2/gameservers/validate")
            {
                return CreateJsonResponse(new GameServerValidationResult
                {
                    IsValid = false,
                    Issues = [ new GameServerValidationIssue { Scope = "settings:SERVER_PORT", Message = "Port is already in use.", IsBlocking = true, Severity = "Error", Code = "PortUnavailable" } ]
                });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        // Act
        var cut = Render<GameServerDetailsV2>(parameters => parameters.Add(p => p.ServerId, "srv-1"));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Current Validation", cut.Markup);
            Assert.Contains("Port is already in use.", cut.Markup);
            Assert.Contains("Minecraft Survival", cut.Markup);
        });
    }

    [Fact]
    public void GameServerEditorV2_New_ShouldRenderRevisionSettings()
    {
        // Arrange
        RegisterApis(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/api/v2/gametypes")
            {
                return CreateJsonResponse(new List<GameTypeListItem>
                {
                    new()
                    {
                        Key = "minecraft",
                        DisplayName = "Minecraft",
                        CurrentRevisionId = 10,
                        CurrentVersionTag = "1.21.2"
                    }
                });
            }

            if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/api/v2/gametypes/minecraft")
            {
                return CreateJsonResponse(new GameTypeDetail
                {
                    Key = "minecraft",
                    DisplayName = "Minecraft",
                    CurrentRevisionId = 10,
                    Revisions =
                    [
                        new GameTypeRevision
                        {
                            Id = 10,
                            VersionTag = "1.21.2",
                            ImageReference = "itzg/minecraft-server",
                            SettingDefinitions =
                            [
                                new GameTypeSettingDefinition
                                {
                                    SettingKey = "SERVER_PORT",
                                    DefaultValue = "25565",
                                    Metadata = new GameTypeSettingMetadata { DataType = "port", IsRequired = true, Category = "Network" }
                                }
                            ]
                        }
                    ]
                });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        // Act
        var cut = Render<GameServerEditorV2>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Create Game Server V2", cut.Markup);
            Assert.Contains("SERVER_PORT", cut.Markup);
            Assert.Contains("Create Server", cut.Markup);
        });
    }

    private void RegisterApis(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        Services.AddSingleton<NotificationService>();
        Services.AddSingleton(CreateGameServerApiService(handler));
        Services.AddSingleton(CreateGameTypeApiService(handler));
    }

    private static GameServerV2ApiService CreateGameServerApiService(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(new StubHttpMessageHandler(handler)) { BaseAddress = new Uri("http://localhost/") });

        return new GameServerV2ApiService(httpClientFactory.Object, CreateOptions());
    }

    private static GameTypeV2ApiService CreateGameTypeApiService(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(new StubHttpMessageHandler(handler)) { BaseAddress = new Uri("http://localhost/") });

        return new GameTypeV2ApiService(httpClientFactory.Object, CreateOptions());
    }

    private static GameServerDockerApi CreateOptions()
    {
        return new GameServerDockerApi
        {
            BaseUri = "http://localhost/"
        };
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
