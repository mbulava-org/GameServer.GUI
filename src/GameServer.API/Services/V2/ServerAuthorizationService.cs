using System.Security.Claims;
using GameServer.API.Data.V2;
using Microsoft.EntityFrameworkCore;

namespace GameServer.API.Services.V2;

public interface IServerAuthorizationService
{
    Task<bool> CanViewServerAsync(ClaimsPrincipal? user, string serverId, CancellationToken cancellationToken = default);
    Task<bool> CanEditServerAsync(ClaimsPrincipal? user, string serverId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>?> GetAccessibleServerIdsAsync(ClaimsPrincipal? user, CancellationToken cancellationToken = default);
    bool CanViewPassword(ClaimsPrincipal? user, int? serverCreatedByUserId, string? accessPolicy, IReadOnlyList<int>? allowedUserIds);
    bool IsAdmin(ClaimsPrincipal? user);
}

public class ServerAuthorizationService(GameServerV2DbContext context, ILogger<ServerAuthorizationService> logger) : IServerAuthorizationService
{
    public bool IsAdmin(ClaimsPrincipal? user)
    {
        if (user is null || user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        return user.IsInRole("Admin");
    }

    public async Task<bool> CanViewServerAsync(ClaimsPrincipal? user, string serverId, CancellationToken cancellationToken = default)
    {
        if (user is null || user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (user.IsInRole("Admin"))
        {
            return true;
        }

        var userId = GetUserId(user);
        if (userId is null)
        {
            return false;
        }

        var hasAccess = await context.GameServers
            .AsNoTracking()
            .Where(s => s.ServerId == serverId && !s.IsDeleted)
            .AnyAsync(s => s.CreatedByUserId == userId.Value ||
                           s.Groups.Any(sg => sg.Group.UserGroups.Any(ug => ug.UserId == userId.Value)), cancellationToken)
            .ConfigureAwait(false);

        return hasAccess;
    }

    public async Task<bool> CanEditServerAsync(ClaimsPrincipal? user, string serverId, CancellationToken cancellationToken = default)
    {
        if (user is null || user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (user.IsInRole("Admin"))
        {
            return true;
        }

        var userId = GetUserId(user);
        if (userId is null)
        {
            return false;
        }

        var hasAccess = await context.GameServers
            .AsNoTracking()
            .Where(s => s.ServerId == serverId && !s.IsDeleted)
            .AnyAsync(s => s.CreatedByUserId == userId.Value ||
                           s.Groups.Any(sg => sg.AccessLevel == "Edit" && sg.Group.UserGroups.Any(ug => ug.UserId == userId.Value)), cancellationToken)
            .ConfigureAwait(false);

        return hasAccess;
    }

    public async Task<IReadOnlyList<string>?> GetAccessibleServerIdsAsync(ClaimsPrincipal? user, CancellationToken cancellationToken = default)
    {
        if (user is null || user.Identity?.IsAuthenticated != true)
        {
            return Array.Empty<string>();
        }

        if (user.IsInRole("Admin"))
        {
            return null; // Admin has access to all servers (no filter)
        }

        var userId = GetUserId(user);
        if (userId is null)
        {
            return Array.Empty<string>();
        }

        var serverIds = await context.GameServers
            .AsNoTracking()
            .Where(s => !s.IsDeleted &&
                        (s.CreatedByUserId == userId.Value ||
                         s.Groups.Any(sg => sg.Group.UserGroups.Any(ug => ug.UserId == userId.Value))))
            .Select(s => s.ServerId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return serverIds;
    }

    public bool CanViewPassword(ClaimsPrincipal? user, int? serverCreatedByUserId, string? accessPolicy, IReadOnlyList<int>? allowedUserIds)
    {
        if (IsAdmin(user))
        {
            return true;
        }

        if (user is null || user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var userId = GetUserId(user);
        if (userId is null)
        {
            return false;
        }

        // The creator of the server always has access to all passwords on their server
        if (serverCreatedByUserId.HasValue && serverCreatedByUserId.Value == userId.Value)
        {
            return true;
        }

        if (string.Equals(accessPolicy, "Individual", StringComparison.OrdinalIgnoreCase))
        {
            return allowedUserIds is not null && allowedUserIds.Contains(userId.Value);
        }

        // "Group" access policy: if they have server access, they can view group passwords
        return true;
    }

    private static int? GetUserId(ClaimsPrincipal user)
    {
        var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(idClaim, out var id))
        {
            return id;
        }

        return null;
    }
}
