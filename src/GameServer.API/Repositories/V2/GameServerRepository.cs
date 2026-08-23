using GameServerModel = GameServer.API.Models.V2.GameServer;
using GameServerSettingModel = GameServer.API.Models.V2.GameServerSetting;
using GameServerVolumeModel = GameServer.API.Models.V2.GameServerVolume;
using Microsoft.EntityFrameworkCore;
using DataV2 = GameServer.API.Data.V2;

namespace GameServer.API.Repositories.V2;

public class GameServerRepository(DataV2.GameServerV2DbContext context, ILogger<GameServerRepository> logger)
    : IGameServerRepository
{
    public async Task<List<GameServerModel>> GetAllAsync(bool includeDeleted = false)
    {
        var query = QueryServers();
        if (!includeDeleted)
        {
            query = query.Where(x => !x.IsDeleted);
        }

        var entities = await query.OrderBy(x => x.Name).ToListAsync();
        return entities.Select(MapToModel).ToList();
    }

    public async Task<GameServerModel?> GetByIdAsync(int id)
    {
        var entity = await QueryServers().FirstOrDefaultAsync(x => x.Id == id);
        return entity is null ? null : MapToModel(entity);
    }

    public async Task<GameServerModel?> GetByServerIdAsync(string serverId)
    {
        var entity = await QueryServers().FirstOrDefaultAsync(x => x.ServerId == serverId);
        return entity is null ? null : MapToModel(entity);
    }

    public async Task<GameServerModel> CreateAsync(GameServerModel server)
    {
        Validate(server);

        var entity = new DataV2.GameServerEntity
        {
            ServerId = server.ServerId,
            Name = server.Name,
            Description = server.Description,
            GameTypeRevisionId = server.GameTypeRevisionId,
            ServiceName = server.ServiceName,
            Status = server.Status,
            CreatedAt = server.CreatedAt == default ? DateTime.UtcNow : server.CreatedAt,
            UpdatedAt = server.UpdatedAt == default ? DateTime.UtcNow : server.UpdatedAt,
            LastDeployedAt = server.LastDeployedAt,
            LastSeenAt = server.LastSeenAt,
            IsDeleted = server.IsDeleted,
            Settings = server.Settings.Select(x => new DataV2.GameServerSettingEntity
            {
                SettingKey = x.SettingKey,
                Value = x.Value
            }).ToList(),
            Volumes = server.Volumes.Select(MapVolumeToEntity).ToList()
        };

        context.GameServers.Add(entity);
        await context.SaveChangesAsync();

        logger.LogInformation("Created V2 GameServer {ServerId}", server.ServerId);
        return await GetByIdAsync(entity.Id) ?? throw new InvalidOperationException("Failed to reload created V2 GameServer");
    }

    public async Task<GameServerModel> UpdateAsync(GameServerModel server)
    {
        Validate(server);

        var entity = await context.GameServers
            .Include(x => x.Settings)
            .FirstOrDefaultAsync(x => x.Id == server.Id || x.ServerId == server.ServerId);

        if (entity is null)
        {
            throw new KeyNotFoundException($"V2 GameServer '{server.ServerId}' was not found");
        }

        entity.Name = server.Name;
        entity.Description = server.Description;
        entity.GameTypeRevisionId = server.GameTypeRevisionId;
        entity.ServiceName = server.ServiceName;
        entity.Status = server.Status;
        entity.LastDeployedAt = server.LastDeployedAt;
        entity.LastSeenAt = server.LastSeenAt;
        entity.IsDeleted = server.IsDeleted;

        entity.Settings.Clear();
        foreach (var setting in server.Settings)
        {
            entity.Settings.Add(new DataV2.GameServerSettingEntity
            {
                GameServerId = entity.Id,
                SettingKey = setting.SettingKey,
                Value = setting.Value
            });
        }

        // Preserve existing volume snapshots; merge new ContainerPaths only.
        var existingVolumes = entity.Volumes.ToList();
        foreach (var incoming in server.Volumes)
        {
            var existing = existingVolumes.FirstOrDefault(v =>
                string.Equals(v.ContainerPath, incoming.ContainerPath, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                entity.Volumes.Add(MapVolumeToEntity(incoming, entity.Id));
            }
        }

        await context.SaveChangesAsync();

        logger.LogInformation("Updated V2 GameServer {ServerId}", entity.ServerId);
        return await GetByIdAsync(entity.Id) ?? throw new InvalidOperationException("Failed to reload updated V2 GameServer");
    }

    public async Task DeleteAsync(string serverId, bool softDelete = true)
    {
        var entity = await context.GameServers.FirstOrDefaultAsync(x => x.ServerId == serverId);
        if (entity is null)
        {
            return;
        }

        if (softDelete)
        {
            entity.IsDeleted = true;
        }
        else
        {
            context.GameServers.Remove(entity);
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Deleted V2 GameServer {ServerId} (softDelete: {SoftDelete})", serverId, softDelete);
    }

    private IQueryable<DataV2.GameServerEntity> QueryServers()
    {
        return context.GameServers
            .Include(x => x.Settings)
            .Include(x => x.Volumes)
            .AsSplitQuery()
            .AsQueryable();
    }

    private static DataV2.GameServerVolumeEntity MapVolumeToEntity(GameServerVolumeModel model, int gameServerId = 0)
    {
        return new DataV2.GameServerVolumeEntity
        {
            GameServerId = gameServerId,
            Usage = model.Usage,
            ContainerPath = model.ContainerPath,
            VolumeName = model.VolumeName,
            MountType = model.MountType.ToString().ToLowerInvariant(),
            ReadOnly = model.ReadOnly,
            DriverOptionsJson = model.DriverOptionsJson,
            IsProvisioned = model.IsProvisioned,
            CreatedAt = model.CreatedAt == default ? DateTime.UtcNow : model.CreatedAt
        };
    }

    private static GameServerVolumeModel MapVolumeToModel(DataV2.GameServerVolumeEntity entity)
    {
        return new GameServerVolumeModel
        {
            Id = entity.Id,
            GameServerId = entity.GameServerId,
            Usage = entity.Usage,
            ContainerPath = entity.ContainerPath,
            VolumeName = entity.VolumeName,
            MountType = entity.MountType,
            ReadOnly = entity.ReadOnly,
            DriverOptionsJson = entity.DriverOptionsJson,
            IsProvisioned = entity.IsProvisioned,
            CreatedAt = entity.CreatedAt
        };
    }

    private static void Validate(GameServerModel server)
    {
        if (string.IsNullOrWhiteSpace(server.ServerId))
        {
            throw new InvalidOperationException("ServerId is required");
        }

        if (string.IsNullOrWhiteSpace(server.Name))
        {
            throw new InvalidOperationException("Name is required");
        }

        if (server.GameTypeRevisionId <= 0)
        {
            throw new InvalidOperationException("GameTypeRevisionId is required");
        }

        if (string.IsNullOrWhiteSpace(server.ServiceName))
        {
            throw new InvalidOperationException("ServiceName is required");
        }
    }

    private static GameServerModel MapToModel(DataV2.GameServerEntity entity)
    {
        return new GameServerModel
        {
            Id = entity.Id,
            ServerId = entity.ServerId,
            Name = entity.Name,
            Description = entity.Description,
            GameTypeRevisionId = entity.GameTypeRevisionId,
            ServiceName = entity.ServiceName,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            LastDeployedAt = entity.LastDeployedAt,
            LastSeenAt = entity.LastSeenAt,
            IsDeleted = entity.IsDeleted,
            Settings = entity.Settings.OrderBy(x => x.SettingKey).Select(x => new GameServerSettingModel
            {
                Id = x.Id,
                SettingKey = x.SettingKey,
                Value = x.Value
            }).ToList(),
            Volumes = entity.Volumes.OrderBy(x => x.CreatedAt).Select(MapVolumeToModel).ToList()
        };
    }
}
