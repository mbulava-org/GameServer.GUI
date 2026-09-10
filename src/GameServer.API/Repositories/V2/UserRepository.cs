using GameServer.API.Data.V2;
using Microsoft.EntityFrameworkCore;

namespace GameServer.API.Repositories.V2;

public class UserRepository(GameServerV2DbContext context, ILogger<UserRepository> logger) : IUserRepository
{
    public async Task<UserEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .Include(u => u.UserGroups)
                .ThenInclude(ug => ug.Group)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<UserEntity?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .Include(u => u.UserGroups)
                .ThenInclude(ug => ug.Group)
            .FirstOrDefaultAsync(u => u.Username == username, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UserEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.Users
            .Include(u => u.UserGroups)
                .ThenInclude(ug => ug.Group)
            .OrderBy(u => u.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<UserEntity> CreateAsync(UserEntity user, IEnumerable<int>? groupIds = null, CancellationToken cancellationToken = default)
    {
        user.CreatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (groupIds is not null)
        {
            var validGroupIds = await context.Groups
                .Where(g => groupIds.Contains(g.Id))
                .Select(g => g.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var gid in validGroupIds)
            {
                context.UserGroups.Add(new UserGroupEntity
                {
                    UserId = user.Id,
                    GroupId = gid,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return (await GetByIdAsync(user.Id, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<UserEntity?> UpdateAsync(int id, Action<UserEntity> updateAction, IEnumerable<int>? groupIds = null, CancellationToken cancellationToken = default)
    {
        var user = await context.Users
            .Include(u => u.UserGroups)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            return null;
        }

        updateAction(user);
        user.UpdatedAt = DateTime.UtcNow;

        if (groupIds is not null)
        {
            var existingGroupIds = user.UserGroups.Select(ug => ug.GroupId).ToHashSet();
            var targetGroupIds = groupIds.ToHashSet();

            var toRemove = user.UserGroups.Where(ug => !targetGroupIds.Contains(ug.GroupId)).ToList();
            foreach (var ug in toRemove)
            {
                context.UserGroups.Remove(ug);
            }

            var toAdd = targetGroupIds.Where(gid => !existingGroupIds.Contains(gid)).ToList();
            var validToAdd = await context.Groups
                .Where(g => toAdd.Contains(g.Id))
                .Select(g => g.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var gid in validToAdd)
            {
                context.UserGroups.Add(new UserGroupEntity
                {
                    UserId = user.Id,
                    GroupId = gid,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await context.Users.FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return false;
        }

        context.Users.Remove(user);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task UpdateLastLoginAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await context.Users.FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (user is not null)
        {
            user.LastLoginAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
