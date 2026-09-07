using System.Security.Claims;
using System.Text;
using System.Text.Json;
using GameServer.Web.Services.Auth;
using Microsoft.JSInterop;
using Moq;
using Xunit;

namespace GameServer.Web.Tests.Services.Auth;

public sealed class JwtAuthenticationStateProviderTests
{
    private readonly Mock<IJSRuntime> _jsRuntime = new();

    private static string Base64UrlEncode(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string GenerateJwt(string userId, string username, string role, long? expSeconds = null)
    {
        var header = Base64UrlEncode(JsonSerializer.Serialize(new { alg = "HS256", typ = "JWT" }));
        
        var exp = expSeconds ?? DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var payloadObj = new Dictionary<string, object>
        {
            ["sub"] = userId,
            ["name"] = username,
            ["role"] = role,
            ["exp"] = exp
        };

        var payload = Base64UrlEncode(JsonSerializer.Serialize(payloadObj));
        var signature = Base64UrlEncode("fake_signature");

        return $"{header}.{payload}.{signature}";
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_WhenNoTokenInStorage_ReturnsAnonymous()
    {
        // Arrange
        _jsRuntime.Setup(j => j.InvokeAsync<string?>("localStorage.getItem", It.Is<object[]>(args => (string)args[0] == "gs_auth_token")))
            .ReturnsAsync((string?)null);

        var provider = new JwtAuthenticationStateProvider(_jsRuntime.Object);

        // Act
        var state = await provider.GetAuthenticationStateAsync();

        // Assert
        Assert.NotNull(state.User);
        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_WhenValidTokenInStorage_ReturnsAuthenticatedUser()
    {
        // Arrange
        var token = GenerateJwt("42", "john_doe", "GameManager");

        _jsRuntime.Setup(j => j.InvokeAsync<string?>("localStorage.getItem", It.Is<object[]>(args => (string)args[0] == "gs_auth_token")))
            .ReturnsAsync(token);

        var provider = new JwtAuthenticationStateProvider(_jsRuntime.Object);

        // Act
        var state = await provider.GetAuthenticationStateAsync();

        // Assert
        Assert.NotNull(state.User);
        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.Equal("john_doe", state.User.Identity?.Name);
        Assert.True(state.User.IsInRole("GameManager"));
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_WhenExpiredTokenInStorage_ReturnsAnonymous()
    {
        // Arrange
        var expiredToken = GenerateJwt("42", "john_doe", "GameManager", DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds());

        _jsRuntime.Setup(j => j.InvokeAsync<string?>("localStorage.getItem", It.Is<object[]>(args => (string)args[0] == "gs_auth_token")))
            .ReturnsAsync(expiredToken);

        var provider = new JwtAuthenticationStateProvider(_jsRuntime.Object);

        // Act
        var state = await provider.GetAuthenticationStateAsync();

        // Assert
        Assert.NotNull(state.User);
        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task MarkUserAsAuthenticatedAsync_SetsTokenAndNotifiesState()
    {
        // Arrange
        var token = GenerateJwt("1", "admin", "Admin");
        var provider = new JwtAuthenticationStateProvider(_jsRuntime.Object);

        bool stateChangedFired = false;
        provider.AuthenticationStateChanged += (task) =>
        {
            stateChangedFired = true;
        };

        // Act
        await provider.MarkUserAsAuthenticatedAsync(token);
        var state = await provider.GetAuthenticationStateAsync();

        // Assert
        Assert.True(stateChangedFired);
        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.Equal("admin", state.User.Identity?.Name);
        Assert.True(state.User.IsInRole("Admin"));
    }

    [Fact]
    public async Task MarkUserAsLoggedOutAsync_ClearsTokenAndNotifiesAnonymous()
    {
        // Arrange
        var provider = new JwtAuthenticationStateProvider(_jsRuntime.Object);

        // Act
        await provider.MarkUserAsLoggedOutAsync();
        var state = await provider.GetAuthenticationStateAsync();

        // Assert
        Assert.False(state.User.Identity?.IsAuthenticated);
    }
}
