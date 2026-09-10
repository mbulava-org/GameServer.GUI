using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace GameServer.Web.Services.Auth;

public class JwtAuthenticationStateProvider(IJSRuntime jsRuntime) : AuthenticationStateProvider
{
    private const string TokenStorageKey = "gs_auth_token";
    private readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());
    private string? _cachedToken;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var token = _cachedToken;
            if (string.IsNullOrWhiteSpace(token))
            {
                token = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", TokenStorageKey);
                _cachedToken = token;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                return new AuthenticationState(_anonymous);
            }

            var claims = ParseClaimsFromJwt(token);
            if (claims is null || !claims.Any())
            {
                await MarkUserAsLoggedOutAsync();
                return new AuthenticationState(_anonymous);
            }

            // Check expiration
            var expClaim = claims.FirstOrDefault(c => c.Type == "exp");
            if (expClaim is not null && long.TryParse(expClaim.Value, out var expSeconds))
            {
                var expTime = DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
                if (expTime <= DateTime.UtcNow)
                {
                    await MarkUserAsLoggedOutAsync();
                    return new AuthenticationState(_anonymous);
                }
            }

            var identity = new ClaimsIdentity(claims, "jwt");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            return new AuthenticationState(_anonymous);
        }
    }

    public async Task<string?> GetTokenAsync()
    {
        if (!string.IsNullOrWhiteSpace(_cachedToken))
        {
            return _cachedToken;
        }

        try
        {
            _cachedToken = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", TokenStorageKey);
            return _cachedToken;
        }
        catch
        {
            return null;
        }
    }

    public async Task MarkUserAsAuthenticatedAsync(string token)
    {
        _cachedToken = token;
        try
        {
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", TokenStorageKey, token);
        }
        catch
        {
            // Ignore during prerender
        }

        var claims = ParseClaimsFromJwt(token) ?? [];
        var identity = new ClaimsIdentity(claims, "jwt");
        var user = new ClaimsPrincipal(identity);

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
    }

    public async Task MarkUserAsLoggedOutAsync()
    {
        _cachedToken = null;
        try
        {
            await jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenStorageKey);
        }
        catch
        {
            // Ignore
        }

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_anonymous)));
    }

    public static IEnumerable<Claim>? ParseClaimsFromJwt(string jwt)
    {
        var claims = new List<Claim>();
        var parts = jwt.Split('.');
        if (parts.Length < 2) return null;

        var payload = parts[1];
        var jsonBytes = ParseBase64WithoutPadding(payload);
        var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);

        if (keyValuePairs is null) return null;

        foreach (var kvp in keyValuePairs)
        {
            var key = kvp.Key;
            var value = kvp.Value;

            if (key == "role" || key == ClaimTypes.Role || key == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
            {
                AddRoleClaims(claims, value);
            }
            else if (key == "groups")
            {
                if (value is JsonElement element && element.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in element.EnumerateArray())
                    {
                        claims.Add(new Claim("groups", item.GetString() ?? string.Empty));
                    }
                }
                else
                {
                    claims.Add(new Claim("groups", value?.ToString() ?? string.Empty));
                }
            }
            else if (key == "nameid" || key == "sub" || key == ClaimTypes.NameIdentifier || key == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, value?.ToString() ?? string.Empty));
            }
            else if (key == "unique_name" || key == "name" || key == ClaimTypes.Name || key == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")
            {
                claims.Add(new Claim(ClaimTypes.Name, value?.ToString() ?? string.Empty));
            }
            else if (key == "email" || key == ClaimTypes.Email || key == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")
            {
                claims.Add(new Claim(ClaimTypes.Email, value?.ToString() ?? string.Empty));
            }
            else
            {
                claims.Add(new Claim(key, value?.ToString() ?? string.Empty));
            }
        }

        return claims;
    }

    private static void AddRoleClaims(List<Claim> claims, object value)
    {
        if (value is JsonElement element && element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                claims.Add(new Claim(ClaimTypes.Role, item.GetString() ?? string.Empty));
            }
        }
        else
        {
            claims.Add(new Claim(ClaimTypes.Role, value?.ToString() ?? string.Empty));
        }
    }

    private static byte[] ParseBase64WithoutPadding(string base64)
    {
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64.Replace('-', '+').Replace('_', '/'));
    }
}
