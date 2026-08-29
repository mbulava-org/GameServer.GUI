using System.Text.Json;
using GameServer.API.Models.V2;
using Microsoft.EntityFrameworkCore;
using DataV2 = GameServer.API.Data.V2;

namespace GameServer.API.Repositories.V2;

public class GameTypeRepository(DataV2.GameServerV2DbContext context, ILogger<GameTypeRepository> logger)
    : IGameTypeRepository
{
    private const string PostgreSqlV2SchemaName = "core";

    /// <summary>
    /// Initialize the V2 database using the same startup pattern as the legacy repository.
    /// </summary>
    public async Task InitializeDatabaseAsync()
    {
        logger.LogInformation("Initializing V2 database...");

        try
        {
            if (context.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
            {
                await InitializePostgreSqlDatabaseAsync().ConfigureAwait(false);
                return;
            }

            await MigrateRelationalDatabaseAsync().ConfigureAwait(false);

            var hasGameTypes = await context.GameTypes.AnyAsync().ConfigureAwait(false);
            if (!hasGameTypes)
            {
                logger.LogInformation("V2 database initialized. No game types found.");
                return;
            }

            var count = await context.GameTypes.CountAsync().ConfigureAwait(false);
            logger.LogInformation("V2 database initialized. Found {Count} game types.", count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing V2 database");
            throw;
        }
    }

    private async Task InitializePostgreSqlDatabaseAsync()
    {
        if (!await context.Database.CanConnectAsync().ConfigureAwait(false))
        {
            throw new InvalidOperationException("Unable to connect to the configured V2 PostgreSQL database.");
        }

        if (!await PostgreSqlTableExistsAsync("GameTypes").ConfigureAwait(false))
        {
            throw new InvalidOperationException("The V2 PostgreSQL schema has not been deployed. Use the pgPacTool database project and `scripts/Deploy-V2PostgresDatabase.ps1` (or `dotnet tool run pgpac publish`) before starting the application.");
        }

        var hasGameTypes = await context.GameTypes.AnyAsync().ConfigureAwait(false);
        if (!hasGameTypes)
        {
            logger.LogInformation("V2 PostgreSQL database initialized. No game types found.");
            return;
        }

        var count = await context.GameTypes.CountAsync().ConfigureAwait(false);
        logger.LogInformation("V2 PostgreSQL database initialized. Found {Count} game types.", count);
    }

	private async Task MigrateRelationalDatabaseAsync()
	{
		// Schema management is owned entirely by the provider-specific EF Core migrations
		// (SqliteMigrations / MySqlMigrations). No hand-rolled schema repair is performed here.
		var pendingMigrations = (await context.Database.GetPendingMigrationsAsync().ConfigureAwait(false)).ToList();
		if (pendingMigrations.Count == 0)
		{
			logger.LogInformation("No pending V2 database migrations to apply.");
			return;
		}

		logger.LogInformation(
			"Applying {Count} pending V2 database migration(s): {Migrations}",
			pendingMigrations.Count,
			string.Join(", ", pendingMigrations));
		await context.Database.MigrateAsync().ConfigureAwait(false);
		logger.LogInformation("V2 database migrations applied successfully.");
	}

	private async Task<bool> PostgreSqlTableExistsAsync(string tableName)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT 1 FROM information_schema.tables WHERE table_schema = @schemaName AND table_name = @tableName LIMIT 1;";

        var schemaParameter = command.CreateParameter();
        schemaParameter.ParameterName = "@schemaName";
        schemaParameter.Value = PostgreSqlV2SchemaName;
        command.Parameters.Add(schemaParameter);

        var tableParameter = command.CreateParameter();
        tableParameter.ParameterName = "@tableName";
        tableParameter.Value = tableName;
        command.Parameters.Add(tableParameter);

        if (command.Connection?.State != System.Data.ConnectionState.Open)
        {
            await command.Connection!.OpenAsync().ConfigureAwait(false);
        }

        return await command.ExecuteScalarAsync().ConfigureAwait(false) is not null;
    }

    public async Task<List<GameType>> GetAllAsync(bool includeInactive = false)
    {
        var query = QueryGameTypes();
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        var entities = await query.OrderBy(x => x.DisplayName).ToListAsync();
        return entities.Select(MapToModel).ToList();
    }

    public async Task<GameType?> GetByKeyAsync(string key)
    {
        var entity = await QueryGameTypes().FirstOrDefaultAsync(x => x.Key == key);
        return entity is null ? null : MapToModel(entity);
    }

    public async Task<GameType?> GetByIdAsync(int id)
    {
        var entity = await QueryGameTypes().FirstOrDefaultAsync(x => x.Id == id);
        return entity is null ? null : MapToModel(entity);
    }

    public async Task<GameType> CreateAsync(GameType gameType)
    {
        ValidateGameType(gameType);

        var entity = new DataV2.GameTypeEntity
        {
            Key = gameType.Key,
            DisplayName = gameType.DisplayName,
            Description = gameType.Description,
            Type = gameType.Type,
            ThumbnailUrl = gameType.ThumbnailUrl,
            DocumentationUrl = gameType.DocumentationUrl,
            IsActive = gameType.IsActive,
            CurrentRevisionId = gameType.CurrentRevisionId,
            Revisions = gameType.Revisions.Select(MapRevisionToEntity).ToList()
        };

        context.GameTypes.Add(entity);
        await context.SaveChangesAsync();

        logger.LogInformation("Created V2 GameType {Key}", gameType.Key);
        return await GetByIdAsync(entity.Id) ?? throw new InvalidOperationException("Failed to reload created V2 GameType");
    }

    public async Task<GameType> UpdateAsync(GameType gameType)
    {
        ValidateGameType(gameType);

        var entity = await context.GameTypes.FirstOrDefaultAsync(x => x.Id == gameType.Id || x.Key == gameType.Key);
        if (entity is null)
        {
            throw new KeyNotFoundException($"V2 GameType '{gameType.Key}' was not found");
        }

        entity.DisplayName = gameType.DisplayName;
        entity.Description = gameType.Description;
        entity.Type = gameType.Type;
        entity.ThumbnailUrl = gameType.ThumbnailUrl;
        entity.DocumentationUrl = gameType.DocumentationUrl;
        entity.IsActive = gameType.IsActive;
        entity.CurrentRevisionId = gameType.CurrentRevisionId;

        await context.SaveChangesAsync();

        logger.LogInformation("Updated V2 GameType {Key}", entity.Key);
        return await GetByIdAsync(entity.Id) ?? throw new InvalidOperationException("Failed to reload updated V2 GameType");
    }

    public async Task DeleteAsync(string key)
    {
        var entity = await context.GameTypes.FirstOrDefaultAsync(x => x.Key == key);
        if (entity is null)
        {
            return;
        }

        context.GameTypes.Remove(entity);
        await context.SaveChangesAsync();
        logger.LogInformation("Deleted V2 GameType {Key}", key);
    }

    public async Task<GameTypeRevision> AddRevisionAsync(string gameTypeKey, GameTypeRevision revision)
    {
        ValidateRevision(revision);

        var gameType = await context.GameTypes
            .Include(x => x.Revisions)
            .FirstOrDefaultAsync(x => x.Key == gameTypeKey);

        if (gameType is null)
        {
            throw new KeyNotFoundException($"V2 GameType '{gameTypeKey}' was not found");
        }

        if (gameType.Revisions.Any(x => string.Equals(x.VersionTag, revision.VersionTag, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.ImageReference, revision.ImageReference, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Image reference '{revision.ImageReference}' with version tag '{revision.VersionTag}' already exists for '{gameTypeKey}'");
        }

        var entity = MapRevisionToEntity(revision);
        entity.GameTypeId = gameType.Id;
        context.GameTypeRevisions.Add(entity);
        gameType.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        logger.LogInformation("Added V2 GameType revision {GameTypeKey}:{VersionTag}", gameTypeKey, revision.VersionTag);
        return await GetRevisionAsync(entity.Id) ?? throw new InvalidOperationException("Failed to reload created V2 GameTypeRevision");
    }

    public async Task<GameTypeRevision> UpdateRevisionAsync(string gameTypeKey, GameTypeRevision revision)
    {
        ValidateRevision(revision);

        var entity = await context.GameTypeRevisions
            .Include(x => x.GameType)
            .Include(x => x.Ports)
            .Include(x => x.Volumes)
            .Include(x => x.SettingDefinitions)
                .ThenInclude(x => x.Metadata)
                    .ThenInclude(x => x!.PortMappings)
            .Include(x => x.WebHosts)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == revision.Id && x.GameType.Key == gameTypeKey);

        if (entity is null)
        {
            throw new KeyNotFoundException($"V2 GameType revision '{revision.Id}' was not found for '{gameTypeKey}'");
        }

        var duplicateVersionTagExists = await context.GameTypeRevisions
            .AnyAsync(x => x.GameTypeId == entity.GameTypeId
                && x.Id != revision.Id
                && x.VersionTag == revision.VersionTag
                && x.ImageReference == revision.ImageReference);

        if (duplicateVersionTagExists)
        {
            throw new InvalidOperationException($"Image reference '{revision.ImageReference}' with version tag '{revision.VersionTag}' already exists for '{gameTypeKey}'");
        }

        entity.VersionTag = revision.VersionTag;
        entity.ImageReference = revision.ImageReference;
        entity.ImageDigest = revision.ImageDigest;
        entity.EnableTTY = revision.EnableTTY;
        entity.ReadyLogPattern = revision.ReadyLogPattern;
        entity.Notes = revision.Notes;
        entity.IsPublished = revision.IsPublished;

        context.GameTypePorts.RemoveRange(entity.Ports);
        context.GameTypeVolumes.RemoveRange(entity.Volumes);
        context.GameTypeSettingDefinitions.RemoveRange(entity.SettingDefinitions);
        context.GameTypeWebHosts.RemoveRange(entity.WebHosts);

        entity.Ports = revision.Ports.Select(x => new DataV2.GameTypePortEntity
        {
            ContainerPort = x.ContainerPort,
            Protocol = x.Protocol,
            AdvertisedPort = x.AdvertisedPort,
            Description = x.Description,
            DisplayOrder = x.DisplayOrder
        }).ToList();

        entity.Volumes = revision.Volumes.Select(x => new DataV2.GameTypeVolumeEntity
        {
            Source = x.Source,
            Description = x.Description,
            DisplayOrder = x.DisplayOrder,
            Usage = x.Usage,
            MountType = x.MountType,
            ReadOnly = x.ReadOnly,
            OwnerUid = x.OwnerUid,
            OwnerGid = x.OwnerGid,
            OwnerUidVariable = x.OwnerUidVariable,
            OwnerGidVariable = x.OwnerGidVariable,
            Permissions = x.Permissions,
            EnsureNfsPathExists = x.EnsureNfsPathExists,
            Required = x.Required
        }).ToList();

        entity.SettingDefinitions = revision.SettingDefinitions.Select(x => new DataV2.GameTypeSettingDefinitionEntity
        {
            SettingKey = x.SettingKey,
            DefaultValue = x.DefaultValue,
            Description = x.Description,
            DisplayOrder = x.DisplayOrder,
            Metadata = x.Metadata is null ? null : new DataV2.GameTypeSettingMetadataEntity
            {
                DataType = NormalizeDataType(x.Metadata.DataType),
                Category = x.Metadata.Category,
                IsRequired = x.Metadata.IsRequired,
                CannotBeEmpty = x.Metadata.CannotBeEmpty,
                Placeholder = x.Metadata.Placeholder,
                ValidationPattern = x.Metadata.ValidationPattern,
                ValidationMessage = x.Metadata.ValidationMessage,
                AutoAllocatePort = x.Metadata.AutoAllocatePort,
                ValidateRelatedPortsAvailability = x.Metadata.ValidateRelatedPortsAvailability,
                AllowedValuesJson = NormalizeJson(x.Metadata.AllowedValuesJson),
                ValueMappingsJson = NormalizeJson(x.Metadata.ValueMappingsJson),
                PortMappings = x.Metadata.PortMappings.Select(pm => new DataV2.GameTypeSettingPortMappingEntity
                {
                    MappingRole = pm.MappingRole,
                    RelationType = pm.RelationType,
                    TargetContainerPort = pm.TargetContainerPort,
                    TargetProtocol = pm.TargetProtocol,
                    CalculationValue = pm.CalculationValue,
                    IsRequired = pm.IsRequired,
                    DisplayOrder = pm.DisplayOrder
                }).ToList()
            }
        }).ToList();

        entity.WebHosts = revision.WebHosts.Select(x => new DataV2.GameTypeWebHostEntity
        {
            Name = x.Name,
            Description = x.Description,
            PathSegment = x.PathSegment,
            ContainerPort = x.ContainerPort,
            ContainerPortVariable = x.ContainerPortVariable,
            EnabledWhen = x.EnabledWhen,
            DisplayOrder = x.DisplayOrder
        }).ToList();

        entity.GameType.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        logger.LogInformation("Updated V2 GameType revision {GameTypeKey}:{VersionTag}", gameTypeKey, revision.VersionTag);
        return await GetRevisionAsync(entity.Id) ?? throw new InvalidOperationException("Failed to reload updated V2 GameTypeRevision");
    }

    public async Task SetCurrentRevisionAsync(string gameTypeKey, int revisionId)
    {
        var gameType = await context.GameTypes
            .Include(x => x.Revisions)
            .FirstOrDefaultAsync(x => x.Key == gameTypeKey);

        if (gameType is null)
        {
            throw new KeyNotFoundException($"V2 GameType '{gameTypeKey}' was not found");
        }

        if (gameType.Revisions.All(x => x.Id != revisionId))
        {
            throw new InvalidOperationException($"Revision '{revisionId}' does not belong to '{gameTypeKey}'");
        }

        gameType.CurrentRevisionId = revisionId;
        await context.SaveChangesAsync();
    }

    private IQueryable<DataV2.GameTypeEntity> QueryGameTypes()
    {
        return context.GameTypes
            .Include(x => x.Revisions)
                .ThenInclude(x => x.Ports)
            .Include(x => x.Revisions)
                .ThenInclude(x => x.Volumes)
            .Include(x => x.Revisions)
                .ThenInclude(x => x.SettingDefinitions)
                    .ThenInclude(x => x.Metadata)
                        .ThenInclude(x => x!.PortMappings)
            .Include(x => x.Revisions)
                .ThenInclude(x => x.WebHosts)
            .AsSplitQuery()
            .AsQueryable();
    }

    private async Task<GameTypeRevision?> GetRevisionAsync(int id)
    {
        var entity = await context.GameTypeRevisions
            .Include(x => x.Ports)
            .Include(x => x.Volumes)
            .Include(x => x.SettingDefinitions)
                .ThenInclude(x => x.Metadata)
                    .ThenInclude(x => x!.PortMappings)
            .Include(x => x.WebHosts)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id);

        return entity is null ? null : MapToModel(entity);
    }

    private static void ValidateGameType(GameType gameType)
    {
        if (string.IsNullOrWhiteSpace(gameType.Key))
        {
            throw new InvalidOperationException("GameType key is required");
        }

        if (string.IsNullOrWhiteSpace(gameType.DisplayName))
        {
            throw new InvalidOperationException("GameType display name is required");
        }

        if (string.IsNullOrWhiteSpace(gameType.Type))
        {
            throw new InvalidOperationException("GameType type is required");
        }

        foreach (var revision in gameType.Revisions)
        {
            ValidateRevision(revision);
        }
    }

    private static void ValidateRevision(GameTypeRevision revision)
    {
        if (string.IsNullOrWhiteSpace(revision.VersionTag))
        {
            throw new InvalidOperationException("Revision version tag is required");
        }

        if (string.IsNullOrWhiteSpace(revision.ImageReference))
        {
            throw new InvalidOperationException("Revision image reference is required");
        }

        if (revision.Ports.Count > 0 && revision.Ports.Count(x => x.AdvertisedPort) != 1)
        {
            throw new InvalidOperationException("Each revision must have exactly one advertised port");
        }
    }

    private static GameType MapToModel(DataV2.GameTypeEntity entity)
    {
        var gameType = new GameType
        {
            Id = entity.Id,
            Key = entity.Key,
            DisplayName = entity.DisplayName,
            Description = entity.Description,
            Type = entity.Type,
            ThumbnailUrl = entity.ThumbnailUrl,
            DocumentationUrl = entity.DocumentationUrl,
            IsActive = entity.IsActive,
            CurrentRevisionId = entity.CurrentRevisionId,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };

        var revisions = entity.Revisions
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.VersionTag)
            .Select(revision => MapToModel(revision))
            .ToList();

        var result = gameType with
        {
            Revisions = revisions
        };

        foreach (var revision in result.Revisions)
        {
            revision.GameType = result;
        }

        return result;
    }

    private static GameTypeRevision MapToModel(DataV2.GameTypeRevisionEntity entity, GameType? gameType = null)
    {
        return new GameTypeRevision
        {
            Id = entity.Id,
            VersionTag = entity.VersionTag,
            ImageReference = entity.ImageReference,
            ImageDigest = entity.ImageDigest,
            EnableTTY = entity.EnableTTY,
            ReadyLogPattern = entity.ReadyLogPattern,
            Notes = entity.Notes,
            IsPublished = entity.IsPublished,
            CreatedAt = entity.CreatedAt,
            GameType = gameType,
            Ports = entity.Ports.OrderBy(x => x.DisplayOrder).Select(x => new GameTypePort
            {
                Id = x.Id,
                ContainerPort = x.ContainerPort,
                Protocol = x.Protocol,
                AdvertisedPort = x.AdvertisedPort,
                Description = x.Description,
                DisplayOrder = x.DisplayOrder
            }).ToList(),
            Volumes = entity.Volumes.OrderBy(x => x.DisplayOrder).Select(x => new GameTypeVolume
            {
                Id = x.Id,
                Source = x.Source,
                Description = x.Description,
                DisplayOrder = x.DisplayOrder,
                Usage = x.Usage,
                MountType = x.MountType,
                ReadOnly = x.ReadOnly,
                OwnerUid = x.OwnerUid,
                OwnerGid = x.OwnerGid,
                OwnerUidVariable = x.OwnerUidVariable,
                OwnerGidVariable = x.OwnerGidVariable,
                Permissions = x.Permissions,
                EnsureNfsPathExists = x.EnsureNfsPathExists,
                Required = x.Required
            }).ToList(),
            SettingDefinitions = entity.SettingDefinitions.OrderBy(x => x.DisplayOrder).Select(x => new GameTypeSettingDefinition
            {
                Id = x.Id,
                SettingKey = x.SettingKey,
                DefaultValue = x.DefaultValue,
                Description = x.Description,
                DisplayOrder = x.DisplayOrder,
                Metadata = x.Metadata is null ? null : new GameTypeSettingMetadata
                {
                    Id = x.Metadata.Id,
                    DataType = x.Metadata.DataType,
                    Category = x.Metadata.Category,
                    IsRequired = x.Metadata.IsRequired,
                    CannotBeEmpty = x.Metadata.CannotBeEmpty,
                    Placeholder = x.Metadata.Placeholder,
                    ValidationPattern = x.Metadata.ValidationPattern,
                    ValidationMessage = x.Metadata.ValidationMessage,
                    AutoAllocatePort = x.Metadata.AutoAllocatePort,
                    ValidateRelatedPortsAvailability = x.Metadata.ValidateRelatedPortsAvailability,
                    AllowedValuesJson = x.Metadata.AllowedValuesJson,
                    ValueMappingsJson = x.Metadata.ValueMappingsJson,
                    PortMappings = x.Metadata.PortMappings.OrderBy(pr => pr.DisplayOrder).Select(pr => new GameTypeSettingPortMapping
                    {
                        Id = pr.Id,
                        MappingRole = pr.MappingRole,
                        RelationType = pr.RelationType,
                        TargetContainerPort = pr.TargetContainerPort,
                        TargetProtocol = pr.TargetProtocol,
                        CalculationValue = pr.CalculationValue,
                        IsRequired = pr.IsRequired,
                        DisplayOrder = pr.DisplayOrder
                    }).ToList()
                }
            }).ToList(),
            WebHosts = entity.WebHosts.OrderBy(x => x.DisplayOrder).Select(x => new GameTypeWebHost
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                PathSegment = x.PathSegment,
                ContainerPort = x.ContainerPort,
                ContainerPortVariable = x.ContainerPortVariable,
                EnabledWhen = x.EnabledWhen,
                DisplayOrder = x.DisplayOrder
            }).ToList()
        };
    }

    private static DataV2.GameTypeRevisionEntity MapRevisionToEntity(GameTypeRevision model)
    {
        return new DataV2.GameTypeRevisionEntity
        {
            VersionTag = model.VersionTag,
            ImageReference = model.ImageReference,
            ImageDigest = model.ImageDigest,
            EnableTTY = model.EnableTTY,
            ReadyLogPattern = model.ReadyLogPattern,
            Notes = model.Notes,
            IsPublished = model.IsPublished,
            CreatedAt = model.CreatedAt == default ? DateTime.UtcNow : model.CreatedAt,
            Ports = model.Ports.Select(x => new DataV2.GameTypePortEntity
            {
                ContainerPort = x.ContainerPort,
                Protocol = x.Protocol,
                AdvertisedPort = x.AdvertisedPort,
                Description = x.Description,
                DisplayOrder = x.DisplayOrder
            }).ToList(),
            Volumes = model.Volumes.Select(x => new DataV2.GameTypeVolumeEntity
            {
                Source = x.Source,
                Description = x.Description,
                DisplayOrder = x.DisplayOrder,
                Usage = x.Usage,
                MountType = x.MountType,
                ReadOnly = x.ReadOnly,
                OwnerUid = x.OwnerUid,
                OwnerGid = x.OwnerGid,
                OwnerUidVariable = x.OwnerUidVariable,
                OwnerGidVariable = x.OwnerGidVariable,
                Permissions = x.Permissions,
                EnsureNfsPathExists = x.EnsureNfsPathExists,
                Required = x.Required
            }).ToList(),
            SettingDefinitions = model.SettingDefinitions.Select(x => new DataV2.GameTypeSettingDefinitionEntity
            {
                SettingKey = x.SettingKey,
                DefaultValue = x.DefaultValue,
                Description = x.Description,
                DisplayOrder = x.DisplayOrder,
                Metadata = x.Metadata is null ? null : new DataV2.GameTypeSettingMetadataEntity
                {
                    DataType = NormalizeDataType(x.Metadata.DataType),
                    Category = x.Metadata.Category,
                    IsRequired = x.Metadata.IsRequired,
                    CannotBeEmpty = x.Metadata.CannotBeEmpty,
                    Placeholder = x.Metadata.Placeholder,
                    ValidationPattern = x.Metadata.ValidationPattern,
                    ValidationMessage = x.Metadata.ValidationMessage,
                    AutoAllocatePort = x.Metadata.AutoAllocatePort,
                    ValidateRelatedPortsAvailability = x.Metadata.ValidateRelatedPortsAvailability,
                    AllowedValuesJson = NormalizeJson(x.Metadata.AllowedValuesJson),
                    ValueMappingsJson = NormalizeJson(x.Metadata.ValueMappingsJson),
                    PortMappings = x.Metadata.PortMappings.Select(pr => new DataV2.GameTypeSettingPortMappingEntity
                    {
                        MappingRole = pr.MappingRole,
                        RelationType = pr.RelationType,
                        TargetContainerPort = pr.TargetContainerPort,
                        TargetProtocol = pr.TargetProtocol,
                        CalculationValue = pr.CalculationValue,
                        IsRequired = pr.IsRequired,
                        DisplayOrder = pr.DisplayOrder
                    }).ToList()
                }
            }).ToList(),
            WebHosts = model.WebHosts.Select(x => new DataV2.GameTypeWebHostEntity
            {
                Name = x.Name,
                Description = x.Description,
                PathSegment = x.PathSegment,
                ContainerPort = x.ContainerPort,
                ContainerPortVariable = x.ContainerPortVariable,
                EnabledWhen = x.EnabledWhen,
                DisplayOrder = x.DisplayOrder
            }).ToList()
        };
    }

    private static string? NormalizeDataType(string? dataType)
    {
        return string.IsNullOrWhiteSpace(dataType) ? null : dataType.ToLowerInvariant();
    }

    private static string? NormalizeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.GetRawText();
        }
        catch (JsonException)
        {
            return json;
        }
    }
}
