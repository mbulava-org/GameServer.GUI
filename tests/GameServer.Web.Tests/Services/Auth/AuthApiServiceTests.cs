using System.Net;
using System.Text;
using System.Text.Json;
using GameServer.Web.Configurations;
using GameServer.Web.Models;
using GameServer.Web.Services.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Moq;
using Xunit;

namespace GameServer.Web.Tests.Services.Auth;

public sealed class AuthApiServiceTests
{
    private static AuthApiService CreateService(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        JwtAuthenticationStateProvider? authStateProvider = null)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new StubHttpMessageHandler(responder))
            {
                BaseAddress = new Uri("http://localhost/")
            });

        var options = new GameServerDockerApi
        {
            BaseUri = "http://localhost/"
        };

        var jsRuntimeMock = new Mock<IJSRuntime>();
        authStateProvider ??= new JwtAuthenticationStateProvider(jsRuntimeMock.Object);

        return new AuthApiService(httpClientFactory.Object, options, authStateProvider, NullLogger<AuthApiService>.Instance);
    }

    [Fact]
    public async Task RegisterAsync_WhenSuccessful_ReturnsTrueAndMessage()
    {
        // Arrange
        var service = CreateService(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/v2/auth/register", request.RequestUri!.AbsolutePath);
            return CreateJsonResponse(new { message = "Account created successfully." }, HttpStatusCode.Created);
        });

        // Act
        var (success, error, message) = await service.RegisterAsync("newuser", "new@example.com", "Password123!");

        // Assert
        Assert.True(success);
        Assert.Null(error);
        Assert.Equal("Account created successfully.", message);
    }

    [Fact]
    public async Task RegisterAsync_WhenUsernameTaken_ReturnsFalseAndError()
    {
        // Arrange
        var service = CreateService(request =>
        {
            return CreateJsonResponse(new { error = "Username 'existing' is already taken." }, HttpStatusCode.BadRequest);
        });

        // Act
        var (success, error, message) = await service.RegisterAsync("existing", null, "Password123!");

        // Assert
        Assert.False(success);
        Assert.Equal("Username 'existing' is already taken.", error);
        Assert.Null(message);
    }

    [Fact]
    public async Task LoginAsync_WhenAccountIsInactive_ReturnsFalseAndPendingMessage()
    {
        // Arrange
        var service = CreateService(request =>
        {
            return CreateJsonResponse(new { error = "Your account is pending activation by an administrator." }, HttpStatusCode.Unauthorized);
        });

        // Act
        var (success, error, response) = await service.LoginAsync("inactiveuser", "Password123!");

        // Assert
        Assert.False(success);
        Assert.Equal("Your account is pending activation by an administrator.", error);
        Assert.Null(response);
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
