using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bunit;
using GameServer.Web.Components.Pages.GameTypes;
using GameServer.Web.Configurations;
using GameServer.Web.Models.V2;
using GameServer.Web.Services.V2;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Radzen;

namespace GameServer.Web.Tests.Components.GameTypes.V2;

public sealed class GameTypeDetailsV2Tests : BunitContext
{
    public GameTypeDetailsV2Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void GameTypeDetailsV2_NewDraft_ShouldRenderUnsavedRevisionRow()
    {
        // Arrange
        var detail = new GameTypeDetail
        {
            Id = 1,
            Key = "minecraft",
            DisplayName = "Minecraft",
            Type = "docker",
            CurrentRevisionId = 11,
            Revisions =
            [
                new GameTypeRevision
                {
                    Id = 11,
                    ImageReference = "itzg/minecraft-server",
                    VersionTag = "1.21",
                    EnableTTY = true,
                    CreatedAt = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                    Ports = [ new GameTypePort { Id = 1, ContainerPort = 25565, Protocol = "tcp", AdvertisedPort = true } ],
                    Volumes = [ new GameTypeVolume { Id = 1, Source = "/data", Usage = "world" } ],
                    SettingDefinitions =
                    [
                        new GameTypeSettingDefinition
                        {
                            Id = 1,
                            SettingKey = "EULA",
                            DefaultValue = "TRUE",
                            Metadata = new GameTypeSettingMetadata { DataType = "boolean" }
                        }
                    ]
                }
            ]
        };

        RegisterApi(detail);

        // Act
        var cut = Render<GameTypeDetailsV2>(parameters => parameters.Add(p => p.Key, "minecraft"));
        cut.WaitForAssertion(() => Assert.Contains("Minecraft", cut.Markup));

        cut.FindAll("a, button").First(element => element.TextContent.Contains("Revisions", StringComparison.Ordinal)).Click();
        cut.FindAll("button").First(button => button.TextContent.Contains("New Draft", StringComparison.Ordinal)).Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Unsaved", cut.Markup);
            Assert.Contains("New Draft", cut.Markup);
            Assert.True(Regex.Matches(cut.Markup, "New Draft").Count >= 2);
        });
    }

    [Fact]
    public void GameTypeDetailsV2_NewDraft_WithoutVersionTagOrPorts_ShouldNotShowDraftCreationBlockers()
    {
        // Arrange
        var detail = new GameTypeDetail
        {
            Id = 1,
            Key = "minecraft",
            DisplayName = "Minecraft",
            Type = "docker",
            Revisions = []
        };

        RegisterApi(detail);

        // Act
        var cut = Render<GameTypeDetailsV2>(parameters => parameters.Add(p => p.Key, "minecraft"));
        cut.WaitForAssertion(() => Assert.Contains("Minecraft", cut.Markup));

        cut.FindAll("a, button").First(element => element.TextContent.Contains("Revisions", StringComparison.Ordinal)).Click();
        cut.FindAll("button").First(button => button.TextContent.Contains("New Draft", StringComparison.Ordinal)).Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Unsaved", cut.Markup);
            Assert.DoesNotContain("Revision version tag is required.", cut.Markup);
            Assert.DoesNotContain("At least one port is required for a revision.", cut.Markup);
            Assert.True(cut.FindAll("button").First(button => button.TextContent.Contains("Save Revision", StringComparison.Ordinal)).HasAttribute("disabled"));
        });
    }

    [Fact]
    public void GameTypeDetailsV2_ReviewTab_ShouldRenderDraftSummaryFromSelectedRevision()
    {
        // Arrange
        var detail = new GameTypeDetail
        {
            Id = 1,
            Key = "minecraft",
            DisplayName = "Minecraft",
            Type = "docker",
            CurrentRevisionId = 11,
            Revisions =
            [
                new GameTypeRevision
                {
                    Id = 11,
                    ImageReference = "itzg/minecraft-server",
                    VersionTag = "1.21",
                    ImageDigest = "sha256:test",
                    EnableTTY = true,
                    IsPublished = true,
                    CreatedAt = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                    Ports = [ new GameTypePort { Id = 1, ContainerPort = 25565, Protocol = "tcp", AdvertisedPort = true } ],
                    Volumes = [ new GameTypeVolume { Id = 1, Source = "/data", Usage = "world" } ],
                    SettingDefinitions =
                    [
                        new GameTypeSettingDefinition
                        {
                            Id = 1,
                            SettingKey = "SERVER_PORT",
                            DefaultValue = "25565",
                            Metadata = new GameTypeSettingMetadata
                            {
                                DataType = "port",
                                PortMappings = [ new GameTypeSettingPortMapping { Id = 1, MappingRole = "Primary", RelationType = "Direct", TargetContainerPort = 25565, TargetProtocol = "tcp" } ]
                            }
                        }
                    ],
                    WebHosts = [ new GameTypeWebHost { Id = 1, Name = "Map", ContainerPort = 25565 } ]
                }
            ]
        };

        RegisterApi(detail);

        // Act
        var cut = Render<GameTypeDetailsV2>(parameters => parameters.Add(p => p.Key, "minecraft"));
        cut.WaitForAssertion(() => Assert.Contains("Minecraft", cut.Markup));

        cut.FindAll("a, button").First(element => element.TextContent.Contains("Review", StringComparison.Ordinal)).Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Revision Draft Summary", cut.Markup);
            Assert.Contains("Version Tag:", cut.Markup);
            Assert.Contains("1.21", cut.Markup);
            Assert.Contains("1 port(s)", cut.Markup);
            Assert.Contains("1 web host(s)", cut.Markup);
            Assert.Contains("1 port mapping rule(s)", cut.Markup);
        });
    }

    [Fact]
    public void GameTypeDetailsV2_CrossTabValidation_ShouldRenderOutsideTabs()
    {
        // Arrange
        var detail = new GameTypeDetail
        {
            Id = 1,
            Key = "minecraft",
            DisplayName = "Minecraft",
            Type = "docker",
            CurrentRevisionId = 11,
            Revisions =
            [
                new GameTypeRevision
                {
                    Id = 11,
                    ImageReference = "itzg/minecraft-server",
                    VersionTag = "1.21",
                    CreatedAt = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                    Ports = [],
                    SettingDefinitions =
                    [
                        new GameTypeSettingDefinition
                        {
                            Id = 1,
                            SettingKey = "SERVER_PORT",
                            DefaultValue = "25565",
                            Metadata = new GameTypeSettingMetadata
                            {
                                DataType = "port",
                                PortMappings = [ new GameTypeSettingPortMapping { Id = 1, MappingRole = "Primary", RelationType = "Direct", TargetContainerPort = 25565, TargetProtocol = "tcp" } ]
                            }
                        }
                    ]
                }
            ]
        };

        RegisterApi(detail);

        // Act
        var cut = Render<GameTypeDetailsV2>(parameters => parameters.Add(p => p.Key, "minecraft"));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Cross-tab revision validation", cut.Markup);
            Assert.Contains("At least one port is required for a revision.", cut.Markup);
            Assert.Contains("Setting 'SERVER_PORT' references missing target port '25565/tcp'.", cut.Markup);
        });
    }

    private void RegisterApi(GameTypeDetail detail)
    {
        Services.AddSingleton<NotificationService>();
        Services.AddSingleton(CreateApiService(detail));
    }

    private GameTypeV2ApiService CreateApiService(GameTypeDetail detail)
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/api/v2/gametypes/minecraft")
            {
                return CreateJsonResponse(detail);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost/")
            });

        var options = Options.Create(new GameServerDockerApi
        {
            BaseUri = "http://localhost/"
        });

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
