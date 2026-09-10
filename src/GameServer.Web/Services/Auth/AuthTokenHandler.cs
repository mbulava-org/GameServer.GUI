using System.Net.Http.Headers;

namespace GameServer.Web.Services.Auth;

public class AuthTokenHandler(JwtAuthenticationStateProvider authStateProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await authStateProvider.GetTokenAsync();
        if (!string.IsNullOrWhiteSpace(token) && request.Headers.Authorization is null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
