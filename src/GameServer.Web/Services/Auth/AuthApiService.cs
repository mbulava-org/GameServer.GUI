using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GameServer.Web.Configurations;
using GameServer.Web.Models;

namespace GameServer.Web.Services.Auth;

public class AuthApiService(
    IHttpClientFactory httpClientFactory,
    GameServerDockerApi apiOptions,
    JwtAuthenticationStateProvider authStateProvider,
    ILogger<AuthApiService> logger) : IAuthApiService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private async Task<HttpClient> CreateClientAsync()
    {
        var client = httpClientFactory.CreateClient();
        var baseUri = apiOptions.BaseUri ?? "http://localhost:5164/";
        if (!baseUri.EndsWith('/'))
        {
            baseUri += "/";
        }
        client.BaseAddress = new Uri(baseUri);

        var token = await authStateProvider.GetTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    public async Task<(bool Success, string? Error, LoginResponse? Response)> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await CreateClientAsync();
            var response = await client.PostAsJsonAsync("api/v2/auth/login", new LoginRequest(username, password), JsonOptions, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions, cancellationToken);
                if (loginResponse != null)
                {
                    await authStateProvider.MarkUserAsAuthenticatedAsync(loginResponse.Token);
                    return (true, null, loginResponse);
                }
            }

            var errorObj = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken).ConfigureAwait(false);
            var errorMsg = errorObj.TryGetProperty("error", out var err) ? err.GetString() : "Invalid username or password.";
            return (false, errorMsg, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during login for user {Username}", username);
            return (false, "Could not connect to authentication service.", null);
        }
    }

    public async Task<(bool Success, string? Error, string? Message)> RegisterAsync(string username, string? email, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await CreateClientAsync();
            var response = await client.PostAsJsonAsync("api/v2/auth/register", new { Username = username, Email = email, Password = password }, JsonOptions, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var successObj = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken).ConfigureAwait(false);
                var message = successObj.TryGetProperty("message", out var msg)
                    ? msg.GetString()
                    : "Account created successfully. Your account is pending activation by an administrator before you can log in.";
                return (true, null, message);
            }

            var errorObj = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken).ConfigureAwait(false);
            var errorMsg = errorObj.TryGetProperty("error", out var err) ? err.GetString() : "Could not complete registration.";
            return (false, errorMsg, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during registration for user {Username}", username);
            return (false, "Could not connect to authentication service.", null);
        }
    }

    public async Task<UserProfile?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await CreateClientAsync();
            return await client.GetFromJsonAsync<UserProfile>("api/v2/auth/me", JsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not fetch current user profile");
            return null;
        }
    }

    public async Task<(bool Success, string? Error)> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await CreateClientAsync();
            var response = await client.PostAsJsonAsync("api/v2/auth/change-password", new ChangePasswordRequest(currentPassword, newPassword), JsonOptions, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var errorObj = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken).ConfigureAwait(false);
            var errorMsg = errorObj.TryGetProperty("error", out var err) ? err.GetString() : "Could not change password.";
            return (false, errorMsg);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error changing password");
            return (false, ex.Message);
        }
    }

    public async Task<IReadOnlyList<UserModel>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await CreateClientAsync();
            return await client.GetFromJsonAsync<IReadOnlyList<UserModel>>("api/v2/users", JsonOptions, cancellationToken) ?? Array.Empty<UserModel>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting users list");
            return Array.Empty<UserModel>();
        }
    }

    public async Task<UserDetailModel?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await CreateClientAsync();
            return await client.GetFromJsonAsync<UserDetailModel>($"api/v2/users/{id}", JsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting user {Id}", id);
            return null;
        }
    }

    public async Task<(bool Success, string? Error, UserDetailModel? User)> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await CreateClientAsync();
            var response = await client.PostAsJsonAsync("api/v2/users", request, JsonOptions, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var user = await response.Content.ReadFromJsonAsync<UserDetailModel>(JsonOptions, cancellationToken);
                return (true, null, user);
            }

            var errorObj = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken).ConfigureAwait(false);
            var errorMsg = errorObj.TryGetProperty("error", out var err) ? err.GetString() : "Failed to create user.";
            return (false, errorMsg, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating user {Username}", request.Username);
            return (false, ex.Message, null);
        }
    }

    public async Task<(bool Success, string? Error, UserDetailModel? User)> UpdateUserAsync(int id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await CreateClientAsync();
            var response = await client.PutAsJsonAsync($"api/v2/users/{id}", request, JsonOptions, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var user = await response.Content.ReadFromJsonAsync<UserDetailModel>(JsonOptions, cancellationToken);
                return (true, null, user);
            }

            var errorObj = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken).ConfigureAwait(false);
            var errorMsg = errorObj.TryGetProperty("error", out var err) ? err.GetString() : "Failed to update user.";
            return (false, errorMsg, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating user {Id}", id);
            return (false, ex.Message, null);
        }
    }

    public async Task<(bool Success, string? Error)> DeleteUserAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await CreateClientAsync();
            var response = await client.DeleteAsync($"api/v2/users/{id}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var errorObj = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken).ConfigureAwait(false);
            var errorMsg = errorObj.TryGetProperty("error", out var err) ? err.GetString() : "Failed to delete user.";
            return (false, errorMsg);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting user {Id}", id);
            return (false, ex.Message);
        }
    }

    public async Task<IReadOnlyList<GroupModel>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await CreateClientAsync();
            return await client.GetFromJsonAsync<IReadOnlyList<GroupModel>>("api/v2/groups", JsonOptions, cancellationToken) ?? Array.Empty<GroupModel>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting groups list");
            return Array.Empty<GroupModel>();
        }
    }

    public async Task<GroupDetailModel?> GetGroupByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await CreateClientAsync();
            return await client.GetFromJsonAsync<GroupDetailModel>($"api/v2/groups/{id}", JsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting group {Id}", id);
            return null;
        }
    }

    public async Task<(bool Success, string? Error, GroupDetailModel? Group)> CreateGroupAsync(CreateGroupRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await CreateClientAsync();
            var response = await client.PostAsJsonAsync("api/v2/groups", request, JsonOptions, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var group = await response.Content.ReadFromJsonAsync<GroupDetailModel>(JsonOptions, cancellationToken);
                return (true, null, group);
            }

            var errorObj = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken).ConfigureAwait(false);
            var errorMsg = errorObj.TryGetProperty("error", out var err) ? err.GetString() : "Failed to create group.";
            return (false, errorMsg, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating group {Name}", request.Name);
            return (false, ex.Message, null);
        }
    }

    public async Task<(bool Success, string? Error, GroupDetailModel? Group)> UpdateGroupAsync(int id, UpdateGroupRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await CreateClientAsync();
            var response = await client.PutAsJsonAsync($"api/v2/groups/{id}", request, JsonOptions, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var group = await response.Content.ReadFromJsonAsync<GroupDetailModel>(JsonOptions, cancellationToken);
                return (true, null, group);
            }

            var errorObj = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken).ConfigureAwait(false);
            var errorMsg = errorObj.TryGetProperty("error", out var err) ? err.GetString() : "Failed to update group.";
            return (false, errorMsg, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating group {Id}", id);
            return (false, ex.Message, null);
        }
    }

    public async Task<(bool Success, string? Error)> DeleteGroupAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await CreateClientAsync();
            var response = await client.DeleteAsync($"api/v2/groups/{id}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var errorObj = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken).ConfigureAwait(false);
            var errorMsg = errorObj.TryGetProperty("error", out var err) ? err.GetString() : "Failed to delete group.";
            return (false, errorMsg);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting group {Id}", id);
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? Error, GroupDetailModel? Group)> SetGroupMembersAsync(int id, IReadOnlyList<int> userIds, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await CreateClientAsync();
            var response = await client.PutAsJsonAsync($"api/v2/groups/{id}/members", new SetGroupMembersRequest(userIds), JsonOptions, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var group = await response.Content.ReadFromJsonAsync<GroupDetailModel>(JsonOptions, cancellationToken);
                return (true, null, group);
            }

            var errorObj = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken).ConfigureAwait(false);
            var errorMsg = errorObj.TryGetProperty("error", out var err) ? err.GetString() : "Failed to set group members.";
            return (false, errorMsg, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting group members for group {Id}", id);
            return (false, ex.Message, null);
        }
    }

    public async Task<(bool Success, string? Error, GroupDetailModel? Group)> SetGroupServersAsync(int id, IReadOnlyList<ServerGroupAssignmentModel> servers, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await CreateClientAsync();
            var response = await client.PutAsJsonAsync($"api/v2/groups/{id}/servers", new SetGroupServersRequest(servers), JsonOptions, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var group = await response.Content.ReadFromJsonAsync<GroupDetailModel>(JsonOptions, cancellationToken);
                return (true, null, group);
            }

            var errorObj = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken).ConfigureAwait(false);
            var errorMsg = errorObj.TryGetProperty("error", out var err) ? err.GetString() : "Failed to set group server access.";
            return (false, errorMsg, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting group server access for group {Id}", id);
            return (false, ex.Message, null);
        }
    }
}
