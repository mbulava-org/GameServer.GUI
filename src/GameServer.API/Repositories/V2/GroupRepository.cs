using GameServer.API.Data.V2;
using GameServer.API.Dtos.V2;
using Microsoft.EntityFrameworkCore;

namespace GameServer.API.Repositories.V2;

public class GroupRepository(GameServerV2DbContext context, ILogger<GroupRepository> logger) : IGroupRepository
{
    public async Task<GroupEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await context.Groups
            .Include(g => g.UserGroups)
                .ThenInclude(ug => ug.User)
            .Include(g => g.ServerGroups)
                .ThenInclude(sg => sg.GameServer)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<GroupEntity?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await context.Groups
            .Include(g => g.UserGroups)
                .ThenInclude(ug => ug.User)
            .Include(g => g.ServerGroups)
                .ThenInclude(sg => sg.GameServer)
            .FirstOrDefaultAsync(g => g.Name == name, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GroupEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.Groups
            .Include(g => g.UserGroups)
                .ThenInclude(ug => ug.User)
            .Include(g => g.ServerGroups)
                .ThenInclude(sg => sg.GameServer)
            .OrderBy(g => g.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<GroupEntity> CreateAsync(GroupEntity group, CancellationToken cancellationToken = default)
    {
        group.CreatedAt = DateTime.UtcNow;
        group.UpdatedAt = DateTime.UtcNow;

        context.Groups.Add(group);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (await GetByIdAsync(group.Id, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<GroupEntity?> UpdateAsync(int id, string name, string? description, CancellationToken cancellationToken = default)
    {
        var group = await context.Groups.FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            return null;
        }

        group.Name = name;
        group.Description = description;
        group.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var group = await context.Groups.FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            return false;
        }

        context.Groups.Remove(group);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task SetMembersAsync(int groupId, IEnumerable<int> userIds, CancellationToken cancellationToken = default)
    {
        var targetUserIds = userIds.ToHashSet();
        var currentMembers = await context.UserGroups
            .Where(ug => ug.GroupId == groupId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var toRemove = currentMembers.Where(ug => !targetUserIds.Contains(ug.UserId)).ToList();
        foreach (var ug in toRemove)
        {
            context.UserGroups.Remove(ug);
        }

        var existingUserIds = currentMembers.Select(ug => ug.UserId).ToHashSet();
        var toAdd = targetUserIds.Where(uid => !existingUserIds.Contains(uid)).ToList();

        var validUserIds = await context.Users
            .Where(u => toAdd.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var uid in validUserIds)
        {
            context.UserGroups.Add(new UserGroupEntity
            {
                GroupId = groupId,
                UserId = uid,
                CreatedAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetServerAccessAsync(int groupId, IEnumerable<ServerGroupAssignmentDto> servers, CancellationToken cancellationToken = default)
    {
        var currentServerGroups = await context.GameServerGroups
            .Where(sg => sg.GroupId == groupId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var sg in currentServerGroups)
        {
            context.GameServerGroups.Remove(sg);
        }

        var serverList = servers.ToList();
        var serverIds = serverList.Select(s => s.ServerId).Distinct().ToList();

        var gameServers = await context.GameServers
            .Where(gs => serverIds.Contains(gs.ServerId))
            .ToDictionaryAsync(gs => gs.ServerId, gs => gs.Id, cancellationToken)
            .ConfigureAwait(false);

        foreach (var assignment in serverList)
        {
            if (gameServers.TryGetValue(assignment.ServerId, out var gameServerId))
            {
                var accessLevel = string.Equals(assignment.AccessLevel, "Edit", StringComparison.OrdinalIgnoreCase) ? "Edit" : "View";
                context.GameServerGroups.Add(new GameServerGroupEntity
                {
                    GroupId = groupId,
                    GameServerId = gameServerId,
                    AccessLevel = accessLevel,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<int>> GetUserGroupIdsAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await context.UserGroups
            .Where(ug => ug.UserId == userId)
            .Select(ug => ug.GroupId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
