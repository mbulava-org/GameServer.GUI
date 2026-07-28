using GameServer.Docker.Data.V2;
using GameServer.Docker.Models.V2;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Docker.Repositories.V2;

public interface IMountTypeConfigRepository
{
    Task<IReadOnlyList<MountTypeConfig>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<MountTypeConfig?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    Task<MountTypeConfig> SaveAsync(MountTypeConfig config, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}

public sealed class MountTypeConfigRepository(GameServerV2DbContext dbContext) : IMountTypeConfigRepository
{
    public async Task<IReadOnlyList<MountTypeConfig>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await dbContext.MountTypeConfigs
            .AsNoTracking()
            .OrderBy(e => e.Key)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToModel).ToList();
    }

    public async Task<MountTypeConfig?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var entity = await dbContext.MountTypeConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Key == key, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToModel(entity);
    }

    public async Task<MountTypeConfig> SaveAsync(MountTypeConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(config.Key);

        var existing = await dbContext.MountTypeConfigs
            .FirstOrDefaultAsync(e => e.Key == config.Key, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            existing = new MountTypeConfigEntity { Key = config.Key };
            dbContext.MountTypeConfigs.Add(existing);
        }

        existing.DisplayName = config.DisplayName;
        existing.Description = config.Description;
        existing.Driver = config.Driver;
        existing.DriverOptionsJson = config.DriverOptionsJson;
        existing.SourcePathTemplate = config.SourcePathTemplate;
        existing.ContainerPathTemplate = config.ContainerPathTemplate;
        existing.DefaultReadOnly = config.DefaultReadOnly;
        existing.DefaultInitMode = config.DefaultInitMode.ToString().ToLowerInvariant();
        existing.DefaultOwnerUid = config.DefaultOwnerUid;
        existing.DefaultOwnerGid = config.DefaultOwnerGid;
        existing.DefaultPermissions = config.DefaultPermissions;
        existing.IsActive = config.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return MapToModel(existing);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var existing = await dbContext.MountTypeConfigs
            .FirstOrDefaultAsync(e => e.Key == key, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            dbContext.MountTypeConfigs.Remove(existing);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static MountTypeConfig MapToModel(MountTypeConfigEntity entity)
    {
        return new MountTypeConfig
        {
            Key = entity.Key,
            DisplayName = entity.DisplayName,
            Description = entity.Description,
            Driver = entity.Driver,
            DriverOptionsJson = entity.DriverOptionsJson,
            SourcePathTemplate = entity.SourcePathTemplate,
            ContainerPathTemplate = entity.ContainerPathTemplate,
            DefaultReadOnly = entity.DefaultReadOnly,
            DefaultInitMode = Enum.Parse<VolumeInitMode>(entity.DefaultInitMode, ignoreCase: true),
            DefaultOwnerUid = entity.DefaultOwnerUid,
            DefaultOwnerGid = entity.DefaultOwnerGid,
            DefaultPermissions = entity.DefaultPermissions,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
