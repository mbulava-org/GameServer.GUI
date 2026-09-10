using System.Security.Claims;
using GameServer.Web.Models;
using GameServer.Web.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace GameServer.Web.Tests.Helpers;

public sealed class TestAuthStateProvider : AuthenticationStateProvider
{
    private readonly AuthenticationState _authState;

    public TestAuthStateProvider(string username = "admin", string role = "Admin")
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role),
            new Claim(ClaimTypes.NameIdentifier, "1")
        ], "TestAuth");
        _authState = new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(_authState);
}

public sealed class AlwaysAuthorizedService : IAuthorizationService
{
    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements)
    {
        return Task.FromResult(AuthorizationResult.Success());
    }

    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
    {
        return Task.FromResult(AuthorizationResult.Success());
    }
}

public static class TestAuthExtensions
{
    public static IServiceCollection AddTestAuthServices(this IServiceCollection services, string username = "admin", string role = "Admin")
    {
        services.AddAuthorizationCore();
        services.AddCascadingAuthenticationState();
        services.AddScoped<AuthenticationStateProvider>(_ => new TestAuthStateProvider(username, role));
        services.AddSingleton<IAuthorizationService, AlwaysAuthorizedService>();

        var mockAuthApi = new Mock<IAuthApiService>();
        mockAuthApi
            .Setup(a => a.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfile(1, username, null, role, [], DateTime.UtcNow, null));
        mockAuthApi
            .Setup(a => a.GetGroupsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        mockAuthApi
            .Setup(a => a.GetUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        services.AddSingleton<IAuthApiService>(mockAuthApi.Object);

        return services;
    }
}
