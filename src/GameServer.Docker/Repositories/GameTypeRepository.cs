using GameServer.Docker.Data;
using GameServer.Docker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using System;
using System.Security.AccessControl;
using System.Text.Json;

namespace GameServer.Docker.Repositories
{
    /// <summary>
    /// Repository implementation using Entity Framework Core and SQLite
    /// </summary>
    public class GameTypeRepository : IGameTypeRepository
    {
        private readonly GameServerDbContext _context;
        private readonly ILogger<GameTypeRepository> _logger;

        public GameTypeRepository(GameServerDbContext context, ILogger<GameTypeRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region Query Methods

        public async Task<List<GameTypeDefinition>> GetAllAsync(bool includeInactive = false)
        {
            var query = _context.GameTypes
                .Include(gt => gt.Ports)
                .Include(gt => gt.Volumes)
                .Include(gt => gt.DefaultSettings)
                    .ThenInclude(ds => ds.SettingsMetadata)
                        .ThenInclude(sm => sm!.PortValidation)
                .Include(gt => gt.DefaultSettings)
                    .ThenInclude(ds => ds.SettingsMetadata)
                        .ThenInclude(sm => sm!.PortRelationships)
                .Include(gt => gt.ExtendedMetadata)
                .AsQueryable();

            if (!includeInactive)
            {
                query = query.Where(gt => gt.IsActive);
            }

            var entities = await query.OrderBy(gt => gt.DisplayName).ToListAsync();
            return entities.Select(MapToModel).ToList();
        }

        public async Task<GameTypeDefinition?> GetByKeyAsync(string key)
        {
            var entity = await _context.GameTypes
                .Include(gt => gt.Ports)
                .Include(gt => gt.Volumes)
                .Include(gt => gt.DefaultSettings)
                    .ThenInclude(ds => ds.SettingsMetadata)
                        .ThenInclude(sm => sm!.PortValidation)
                .Include(gt => gt.DefaultSettings)
                    .ThenInclude(ds => ds.SettingsMetadata)
                        .ThenInclude(sm => sm!.PortRelationships)
                .Include(gt => gt.ExtendedMetadata)
                .FirstOrDefaultAsync(gt => gt.Key == key);

            return entity == null ? null : MapToModel(entity);
        }

        public async Task<GameTypeDefinition?> GetByIdAsync(int id)
        {
            var entity = await _context.GameTypes
                .Include(gt => gt.Ports)
                .Include(gt => gt.Volumes)
                .Include(gt => gt.DefaultSettings)
                .Include(gt => gt.ExtendedMetadata)
                .FirstOrDefaultAsync(gt => gt.Id == id);

            return entity == null ? null : MapToModel(entity);
        }

        public async Task<List<GameTypeDefinition>> SearchAsync(string searchTerm)
        {
            var query = _context.GameTypes
                .Include(gt => gt.Ports)
                .Include(gt => gt.Volumes)
                .Include(gt => gt.DefaultSettings)
                .Include(gt => gt.ExtendedMetadata)
                .Where(gt => gt.IsActive &&
                    (gt.Key.Contains(searchTerm) ||
                     gt.DisplayName.Contains(searchTerm) ||
                     (gt.Description != null && gt.Description.Contains(searchTerm))));

            var entities = await query.OrderBy(gt => gt.DisplayName).ToListAsync();
            return entities.Select(MapToModel).ToList();
        }

        public async Task<List<GameTypeDefinition>> GetWithTTYEnabledAsync()
        {
            var query = _context.GameTypes
                .Include(gt => gt.Ports)
                .Include(gt => gt.Volumes)
                .Include(gt => gt.DefaultSettings)
                .Include(gt => gt.ExtendedMetadata)
                .Where(gt => gt.IsActive && gt.ExtendedMetadata != null && gt.ExtendedMetadata.EnableTTY);

            var entities = await query.OrderBy(gt => gt.DisplayName).ToListAsync();
            return entities.Select(MapToModel).ToList();
        }

        public async Task<bool> ExistsAsync(string key)
        {
            return await _context.GameTypes.AnyAsync(gt => gt.Key == key);
        }

        #endregion

        #region CRUD Operations

        public async Task<GameTypeDefinition> CreateAsync(GameTypeDefinition gameType)
        {
            var entity = MapToEntity(gameType);
            _context.GameTypes.Add(entity);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Created GameType: {Key}", gameType.Key);
            return MapToModel(entity);
        }

        public async Task<GameTypeDefinition> UpdateAsync(GameTypeDefinition gameType)
        {
            var entity = await _context.GameTypes
                .Include(gt => gt.Ports)
                .Include(gt => gt.Volumes)
                .Include(gt => gt.DefaultSettings)
                .FirstOrDefaultAsync(gt => gt.Key == gameType.Key);

            if (entity == null)
            {
                throw new KeyNotFoundException($"GameType with key '{gameType.Key}' not found");
            }

            UpdateEntity(entity, gameType);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Updated GameType: {Key}", gameType.Key);
            return MapToModel(entity);
        }

        public async Task DeleteAsync(string key)
        {
            var entity = await _context.GameTypes.FirstOrDefaultAsync(gt => gt.Key == key);
            if (entity != null)
            {
                _context.GameTypes.Remove(entity);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Deleted GameType: {Key}", key);
            }
        }

        #endregion

        #region Extended Metadata

        public async Task<GameTypeExtendedMetadata?> GetExtendedMetadataAsync(string gameTypeKey)
        {
            var gameType = await _context.GameTypes
                .Include(gt => gt.ExtendedMetadata)
                .Include(gt => gt.DefaultSettings)
                    .ThenInclude(ds => ds.SettingsMetadata)
                        .ThenInclude(sm => sm!.PortValidation)
                .Include(gt => gt.DefaultSettings)
                    .ThenInclude(ds => ds.SettingsMetadata)
                        .ThenInclude(sm => sm!.PortRelationships)
                .FirstOrDefaultAsync(gt => gt.Key == gameTypeKey);

            if (gameType?.ExtendedMetadata == null)
                return null;

            return MapExtendedMetadataToModel(gameType);
        }

        public async Task<GameTypeExtendedMetadata> SaveExtendedMetadataAsync(string gameTypeKey, GameTypeExtendedMetadata metadata)
        {
            var gameType = await _context.GameTypes
                .Include(gt => gt.ExtendedMetadata)
                .Include(gt => gt.DefaultSettings)
                    .ThenInclude(ds => ds.SettingsMetadata)
                        .ThenInclude(sm => sm!.PortValidation)
                .Include(gt => gt.DefaultSettings)
                    .ThenInclude(ds => ds.SettingsMetadata)
                        .ThenInclude(sm => sm!.PortRelationships)
                .FirstOrDefaultAsync(gt => gt.Key == gameTypeKey);

            if (gameType == null)
            {
                throw new KeyNotFoundException($"GameType with key '{gameTypeKey}' not found");
            }

            // Update or create ExtendedMetadata
            if (gameType.ExtendedMetadata == null)
            {
                gameType.ExtendedMetadata = new ExtendedMetadataEntity
                {
                    GameTypeId = gameType.Id
                };
            }

            gameType.ExtendedMetadata.EnableTTY = metadata.EnableTTY;
            gameType.ExtendedMetadata.CustomPropertiesJson = metadata.CustomProperties != null
                ? JsonSerializer.Serialize(metadata.CustomProperties)
                : null;

            // Update settings metadata
            foreach (var settingMeta in metadata.SettingsMetadata)
            {
                await UpdateSettingMetadataInternalAsync(gameType, settingMeta.Key, settingMeta.Value);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Saved ExtendedMetadata for GameType: {Key}", gameTypeKey);

            return MapExtendedMetadataToModel(gameType);
        }

        public async Task DeleteExtendedMetadataAsync(string gameTypeKey)
        {
            var gameType = await _context.GameTypes
                .Include(gt => gt.ExtendedMetadata)
                .FirstOrDefaultAsync(gt => gt.Key == gameTypeKey);

            if (gameType?.ExtendedMetadata != null)
            {
                _context.ExtendedMetadata.Remove(gameType.ExtendedMetadata);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Deleted ExtendedMetadata for GameType: {Key}", gameTypeKey);
            }
        }

        #endregion

        #region Setting Metadata

        public async Task<SettingMetadata?> GetSettingMetadataAsync(string gameTypeKey, string settingKey)
        {
            var setting = await _context.DefaultSettings
                .Include(ds => ds.SettingsMetadata)
                    .ThenInclude(sm => sm!.PortValidation)
                .Include(ds => ds.SettingsMetadata)
                    .ThenInclude(sm => sm!.PortRelationships)
                .FirstOrDefaultAsync(ds => 
                    ds.GameType.Key == gameTypeKey && 
                    ds.SettingKey == settingKey);

            if (setting?.SettingsMetadata == null)
                return null;

            return MapSettingMetadataToModel(setting.SettingsMetadata);
        }

        public async Task<Dictionary<string, SettingMetadata>> GetAllSettingMetadataAsync(string gameTypeKey)
        {
            var settings = await _context.DefaultSettings
                .Include(ds => ds.SettingsMetadata)
                    .ThenInclude(sm => sm!.PortValidation)
                .Include(ds => ds.SettingsMetadata)
                    .ThenInclude(sm => sm!.PortRelationships)
                .Where(ds => ds.GameType.Key == gameTypeKey && ds.SettingsMetadata != null)
                .ToListAsync();

            var result = new Dictionary<string, SettingMetadata>();
            foreach (var setting in settings)
            {
                if (setting.SettingsMetadata != null)
                {
                    result[setting.SettingKey] = MapSettingMetadataToModel(setting.SettingsMetadata);
                }
            }

            return result;
        }

        public async Task UpdateSettingMetadataAsync(string gameTypeKey, string settingKey, SettingMetadata metadata)
        {
            var gameType = await _context.GameTypes
                .Include(gt => gt.DefaultSettings)
                    .ThenInclude(ds => ds.SettingsMetadata)
                        .ThenInclude(sm => sm!.PortValidation)
                .Include(gt => gt.DefaultSettings)
                    .ThenInclude(ds => ds.SettingsMetadata)
                        .ThenInclude(sm => sm!.PortRelationships)
                .FirstOrDefaultAsync(gt => gt.Key == gameTypeKey);

            if (gameType == null)
            {
                throw new KeyNotFoundException($"GameType with key '{gameTypeKey}' not found");
            }

            await UpdateSettingMetadataInternalAsync(gameType, settingKey, metadata);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Updated SettingMetadata for {GameType}.{Setting}", gameTypeKey, settingKey);
        }

        public async Task DeleteSettingMetadataAsync(string gameTypeKey, string settingKey)
        {
            var setting = await _context.DefaultSettings
                .Include(ds => ds.SettingsMetadata)
                .FirstOrDefaultAsync(ds => 
                    ds.GameType.Key == gameTypeKey && 
                    ds.SettingKey == settingKey);

            if (setting?.SettingsMetadata != null)
            {
                _context.SettingsMetadata.Remove(setting.SettingsMetadata);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Deleted SettingMetadata for {GameType}.{Setting}", gameTypeKey, settingKey);
            }
        }

        #endregion

        #region Private Helper Methods

        private async Task UpdateSettingMetadataInternalAsync(GameTypeEntity gameType, string settingKey, SettingMetadata metadata)
        {
            var setting = gameType.DefaultSettings.FirstOrDefault(ds => ds.SettingKey == settingKey);
            if (setting == null)
            {
                // Create the default setting if it doesn't exist
                setting = new DefaultSettingEntity
                {
                    GameTypeId = gameType.Id,
                    SettingKey = settingKey,
                    SettingValue = "", // Default empty, user must provide value
                    Description = metadata.Description
                };
                gameType.DefaultSettings.Add(setting);
                await _context.SaveChangesAsync(); // Save to get the ID
            }

            // Update or create settings metadata
            if (setting.SettingsMetadata == null)
            {
                setting.SettingsMetadata = new SettingMetadataEntity
                {
                    DefaultSettingId = setting.Id
                };
            }

            var metaEntity = setting.SettingsMetadata;
            metaEntity.Description = metadata.Description;
            metaEntity.IsRequired = metadata.IsRequired;
            metaEntity.CannotBeEmpty = metadata.CannotBeEmpty;

            // Validate and normalize DataType
            metaEntity.DataType = NormalizeDataType(metadata.DataType);

            metaEntity.Category = metadata.Category;
            metaEntity.DisplayOrder = metadata.DisplayOrder;
            metaEntity.Placeholder = metadata.Placeholder;
            metaEntity.ValidationPattern = metadata.ValidationPattern;
            metaEntity.ValidationMessage = metadata.ValidationMessage;
            metaEntity.MapsToContainerPort = metadata.MapsToContainerPort;
            metaEntity.LinkedContainerPort = metadata.LinkedContainerPort.HasValue 
                ? (int)metadata.LinkedContainerPort.Value 
                : null;
            metaEntity.PortProtocol = metadata.PortProtocol;
            metaEntity.SynchronizedWithSetting = metadata.SynchronizedWithSetting;
            metaEntity.AutoAllocatePort = metadata.AutoAllocatePort;
            metaEntity.ValidateRelatedPortsAvailability = metadata.ValidateRelatedPortsAvailability;
            metaEntity.ListDelimiter = metadata.ListDelimiter;
            metaEntity.AllowedValuesJson = metadata.AllowedValues != null
                ? JsonSerializer.Serialize(metadata.AllowedValues)
                : null;
            metaEntity.ValueMappingsJson = metadata.ValueMappings != null
                ? JsonSerializer.Serialize(metadata.ValueMappings)
                : null;

            // Update port validation
            if (metadata.PortValidation != null)
            {
                if (metaEntity.PortValidation == null)
                {
                    metaEntity.PortValidation = new PortValidationEntity
                    {
                        SettingMetadataId = metaEntity.Id
                    };
                }

                var valEntity = metaEntity.PortValidation;
                valEntity.MinPort = (int)metadata.PortValidation.MinPort;
                valEntity.MaxPort = (int)metadata.PortValidation.MaxPort;
                valEntity.ReservedPortsJson = metadata.PortValidation.ReservedPorts != null
                    ? JsonSerializer.Serialize(metadata.PortValidation.ReservedPorts)
                    : null;
                valEntity.CheckAvailability = metadata.PortValidation.CheckAvailability;
                valEntity.IsUserEditable = metadata.PortValidation.IsUserEditable;
                valEntity.SuggestedPortsJson = metadata.PortValidation.SuggestedPorts != null
                    ? JsonSerializer.Serialize(metadata.PortValidation.SuggestedPorts)
                    : null;
                valEntity.ValidationMessage = metadata.PortValidation.ValidationMessage;
            }

            // Update port relationships
            metaEntity.PortRelationships.Clear();
            if (metadata.PortRelationships != null)
            {
                foreach (var rel in metadata.PortRelationships)
                {
                    metaEntity.PortRelationships.Add(new PortRelationshipEntity
                    {
                        SettingMetadataId = metaEntity.Id,
                        RelationType = (int)rel.RelationType,
                        TargetContainerPort = (int)rel.TargetContainerPort,
                        TargetProtocol = rel.TargetProtocol,
                        OffsetValue = rel.Offset,
                        FixedValue = (int?)rel.FixedValue,
                        Description = rel.Description,
                        IsRequired = rel.IsRequired,
                        DisplayOrder = 0
                    });
                }
            }
        }

        /// <summary>
        /// Normalizes DataType values to lowercase for consistency.
        /// No longer enforces valid types - application layer handles validation.
        /// </summary>
        private static string? NormalizeDataType(string? dataType)
        {
            if (string.IsNullOrWhiteSpace(dataType))
            {
                return null;
            }

            return dataType.ToLowerInvariant();
        }

        private GameTypeDefinition MapToModel(GameTypeEntity entity)
        {
            return new GameTypeDefinition
            {
                Key = entity.Key,
                DisplayName = entity.DisplayName,
                Description = entity.Description ?? "",
                Image = entity.Image,
                ThumbnailUrl = entity.ThumbnailUrl,
                DocumentationUrl = entity.DocumentationUrl,
                Ports = entity.Ports.Select(p => new PortDefinition
                {
                    Port = (uint)p.Port,
                    Protocol = p.Protocol,
                    IsDefaultPort = p.IsDefaultPort
                }).OrderBy(p => p.Port).ToList(),
                Volumes = entity.Volumes.Select(v => new VolumeDefinition
                {
                    Source = v.Source,
                    Target = v.Target
                }).OrderBy(v => v.Target).ToList(),
                DefaultSettings = entity.DefaultSettings.ToDictionary(
                    ds => ds.SettingKey,
                    ds => ds.SettingValue ?? string.Empty
                )
            };
        }

        private GameTypeEntity MapToEntity(GameTypeDefinition model)
        {
            return new GameTypeEntity
            {
                Key = model.Key,
                DisplayName = model.DisplayName,
                Description = model.Description,
                Image = model.Image,
                ThumbnailUrl = model.ThumbnailUrl,
                DocumentationUrl = model.DocumentationUrl,
                IsActive = true,
                Ports = model.Ports?.Select(p => new PortEntity
                {
                    Port = (int)p.Port,
                    Protocol = p.Protocol,
                    IsDefaultPort = p.IsDefaultPort,
                    DisplayOrder = 0
                }).ToList() ?? new List<PortEntity>(),
                Volumes = model.Volumes?.Select(v => new VolumeEntity
                {
                    Source = v.Source,
                    Target = v.Target,
                    ReadOnly = false,  // Default value
                    DisplayOrder = 0
                }).ToList() ?? new List<VolumeEntity>(),
                DefaultSettings = model.DefaultSettings?.Select(ds => new DefaultSettingEntity
                {
                    SettingKey = ds.Key,
                    SettingValue = ds.Value,
                    DisplayOrder = 0
                }).ToList() ?? new List<DefaultSettingEntity>()
            };
        }

        private void UpdateEntity(GameTypeEntity entity, GameTypeDefinition model)
        {
            entity.DisplayName = model.DisplayName;
            entity.Description = model.Description;
            entity.Image = model.Image;
            entity.ThumbnailUrl = model.ThumbnailUrl;
            entity.DocumentationUrl = model.DocumentationUrl;

            // Update ports
            entity.Ports.Clear();
            if (model.Ports != null)
            {
                foreach (var port in model.Ports)
                {
                    entity.Ports.Add(new PortEntity
                    {
                        GameTypeId = entity.Id,
                        Port = (int)port.Port,
                        Protocol = port.Protocol,
                        IsDefaultPort = port.IsDefaultPort,
                        DisplayOrder = 0
                    });
                }
            }

            // Update volumes
            entity.Volumes.Clear();
            if (model.Volumes != null)
            {
                foreach (var volume in model.Volumes)
                {
                    entity.Volumes.Add(new VolumeEntity
                    {
                        GameTypeId = entity.Id,
                        Source = volume.Source,
                        Target = volume.Target,
                        ReadOnly = false,  // Default value
                        DisplayOrder = 0
                    });
                }
            }

            // Update settings
            entity.DefaultSettings.Clear();
            if (model.DefaultSettings != null)
            {
                foreach (var setting in model.DefaultSettings)
                {
                    entity.DefaultSettings.Add(new DefaultSettingEntity
                    {
                        GameTypeId = entity.Id,
                        SettingKey = setting.Key,
                        SettingValue = setting.Value,
                        DisplayOrder = 0
                    });
                }
            }
        }

        private GameTypeExtendedMetadata MapExtendedMetadataToModel(GameTypeEntity gameType)
        {
            var metadata = new GameTypeExtendedMetadata
            {
                GameTypeKey = gameType.Key,
                EnableTTY = gameType.ExtendedMetadata?.EnableTTY ?? false,
                CustomProperties = !string.IsNullOrEmpty(gameType.ExtendedMetadata?.CustomPropertiesJson)
                    ? JsonSerializer.Deserialize<Dictionary<string, string>>(gameType.ExtendedMetadata!.CustomPropertiesJson) ?? new Dictionary<string, string>()
                    : new Dictionary<string, string>(),
                SettingsMetadata = new Dictionary<string, SettingMetadata>()
            };

            // Add settings metadata
            foreach (var setting in gameType.DefaultSettings.Where(ds => ds.SettingsMetadata != null))
            {
                metadata.SettingsMetadata[setting.SettingKey] = MapSettingMetadataToModel(setting.SettingsMetadata!);
            }

            return metadata;
        }

        private SettingMetadata MapSettingMetadataToModel(SettingMetadataEntity entity)
        {
            var model = new SettingMetadata
            {
                Key = entity.DefaultSetting?.SettingKey ?? "",
                Description = entity.Description ?? "",
                IsRequired = entity.IsRequired,
                CannotBeEmpty = entity.CannotBeEmpty,
                DataType = entity.DataType,
                Category = entity.Category,
                DisplayOrder = entity.DisplayOrder,
                Placeholder = entity.Placeholder,
                ValidationPattern = entity.ValidationPattern,
                ValidationMessage = entity.ValidationMessage,
                MapsToContainerPort = entity.MapsToContainerPort,
                LinkedContainerPort = entity.LinkedContainerPort.HasValue 
                    ? (uint)entity.LinkedContainerPort.Value 
                    : null,
                PortProtocol = entity.PortProtocol,
                SynchronizedWithSetting = entity.SynchronizedWithSetting,
                AutoAllocatePort = entity.AutoAllocatePort,
                ValidateRelatedPortsAvailability = entity.ValidateRelatedPortsAvailability,
                ListDelimiter = entity.ListDelimiter,
                AllowedValues = !string.IsNullOrEmpty(entity.AllowedValuesJson)
                    ? JsonSerializer.Deserialize<List<string>>(entity.AllowedValuesJson)
                    : null,
                ValueMappings = !string.IsNullOrEmpty(entity.ValueMappingsJson)
                    ? JsonSerializer.Deserialize<Dictionary<string, string>>(entity.ValueMappingsJson)
                    : null
            };

            // Map port validation
            if (entity.PortValidation != null)
            {
                model.PortValidation = new PortValidationRule
                {
                    MinPort = (uint)entity.PortValidation.MinPort,
                    MaxPort = (uint)entity.PortValidation.MaxPort,
                    ReservedPorts = !string.IsNullOrEmpty(entity.PortValidation.ReservedPortsJson)
                        ? JsonSerializer.Deserialize<List<uint>>(entity.PortValidation.ReservedPortsJson)
                        : null,
                    CheckAvailability = entity.PortValidation.CheckAvailability,
                    IsUserEditable = entity.PortValidation.IsUserEditable,
                    SuggestedPorts = !string.IsNullOrEmpty(entity.PortValidation.SuggestedPortsJson)
                        ? JsonSerializer.Deserialize<List<uint>>(entity.PortValidation.SuggestedPortsJson)
                        : null,
                    ValidationMessage = entity.PortValidation.ValidationMessage
                };
            }

            // Map port relationships
            if (entity.PortRelationships?.Any() == true)
            {
                model.PortRelationships = entity.PortRelationships.Select(pr => new PortRelationship
                {
                    RelationType = (PortRelationshipType)pr.RelationType,
                    TargetContainerPort = (uint)pr.TargetContainerPort,
                    TargetProtocol = pr.TargetProtocol,
                    Offset = pr.OffsetValue,
                    FixedValue = (uint?)pr.FixedValue,
                    Description = pr.Description,
                    IsRequired = pr.IsRequired
                }).ToList();
            }

            return model;
        }

        #endregion


        /// <summary>
        /// Initialize and migrate the database. Should be called once during application startup.
        /// </summary>
        public async Task InitializeDatabaseAsync()
        {
            _logger.LogInformation("Initializing database...");

            try
            {
                // Check if database can connect
                var canConnect = await _context.Database.CanConnectAsync();

                if (!canConnect)
                {
                    // Database doesn't exist - create it with migrations
                    _logger.LogInformation("Database does not exist. Creating with migrations...");
                    await _context.Database.MigrateAsync();
                    _logger.LogInformation("Database created successfully with migrations");
                }
                else
                {
                    // Database exists - check if it has migrations history
                    var hasMigrationsTable = await HasMigrationsHistoryTableAsync();

                    if (!hasMigrationsTable)
                    {
                        // Database was created with EnsureCreated() - need to manually drop constraint
                        _logger.LogWarning("Database exists but was not created with migrations. Attempting to fix schema...");
                        await FixLegacyDatabaseSchemaAsync();
                    }

                    // Now apply any pending migrations
                    _logger.LogInformation("Applying any pending database migrations...");
                    await _context.Database.MigrateAsync();
                    _logger.LogInformation("Database migrations applied successfully");
                }

                // Use AnyAsync() instead of CountAsync() - much faster!
                var hasGameTypes = await _context.GameTypes.AnyAsync();

                if (!hasGameTypes)
                {
                    _logger.LogInformation("Database is empty. Checking for existing JSON files to migrate...");
                    await MigrateFromJsonIfExistsAsync();
                }
                else
                {
                    // Only count if we need to log it (optional)
                    var count = await _context.GameTypes.CountAsync();
                    _logger.LogInformation("Database initialized. Found {Count} game types.", count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing database");
                throw;
            }
        }

        /// <summary>
        /// Check if the __EFMigrationsHistory table exists
        /// </summary>
        private async Task<bool> HasMigrationsHistoryTableAsync()
        {
            try
            {
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory'";
                var result = await command.ExecuteScalarAsync();

                return result != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Fix legacy database schema that was created with EnsureCreated() instead of migrations
        /// </summary>
        private async Task FixLegacyDatabaseSchemaAsync()
        {
            _logger.LogInformation("Fixing legacy database schema...");

            try
            {
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();

                // Drop the constraint if it exists
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    -- Create a new table without the constraint
                    CREATE TABLE IF NOT EXISTS SettingsMetadata_New (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        DefaultSettingId INTEGER NOT NULL,
                        Description TEXT,
                        IsRequired INTEGER NOT NULL DEFAULT 0,
                        CannotBeEmpty INTEGER NOT NULL DEFAULT 0,
                        DataType TEXT,
                        Category TEXT,
                        DisplayOrder INTEGER NOT NULL DEFAULT 0,
                        Placeholder TEXT,
                        ValidationPattern TEXT,
                        ValidationMessage TEXT,
                        MapsToContainerPort INTEGER NOT NULL DEFAULT 0,
                        LinkedContainerPort INTEGER,
                        PortProtocol TEXT NOT NULL DEFAULT 'tcp',
                        SynchronizedWithSetting TEXT,
                        AutoAllocatePort INTEGER NOT NULL DEFAULT 0,
                        ValidateRelatedPortsAvailability INTEGER NOT NULL DEFAULT 1,
                        ListDelimiter TEXT NOT NULL DEFAULT ',',
                        AllowedValuesJson TEXT,
                        ValueMappingsJson TEXT,
                        FOREIGN KEY (DefaultSettingId) REFERENCES DefaultSettings(Id) ON DELETE CASCADE
                    );

                    -- Copy data
                    INSERT OR IGNORE INTO SettingsMetadata_New 
                    SELECT * FROM SettingsMetadata;

                    -- Drop old table and rename new one
                    DROP TABLE IF EXISTS SettingsMetadata;
                    ALTER TABLE SettingsMetadata_New RENAME TO SettingsMetadata;
                ";

                await command.ExecuteNonQueryAsync();
                _logger.LogInformation("Legacy database schema fixed - constraint removed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fix legacy database schema");
                // Don't throw - let migrations try anyway
            }
        }

        /// <summary>
        /// Migrate data from existing JSON files if they exist
        /// </summary>
        private async Task MigrateFromJsonIfExistsAsync()
        {
            var typesFilesPath = "/data/game-types.json";
            var gameTypesDir = "/data/game-types-extended";

            if (!File.Exists(typesFilesPath))
            {
                _logger.LogInformation("No game-types.json file found to migrate.");
                return;
            }

            //import existing Types
            var json = await File.ReadAllTextAsync(typesFilesPath);
            var gameTypes = JsonSerializer.Deserialize<Dictionary<string, GameTypeDefinition>>(json);
            if (gameTypes == null || gameTypes.Count == 0)
            {
                _logger.LogInformation("No game types found in game-types.json to migrate.");
                return;
            }
            //Do the Work
            int migrated = 0;
            foreach (var game in gameTypes)
            {
                var extendedFilePath = Path.Combine(gameTypesDir, $"{game.Key}.json");
                var extendedMetadata = File.Exists(extendedFilePath) ?
                    JsonSerializer.Deserialize<GameTypeExtendedMetadata>(await File.ReadAllTextAsync(extendedFilePath)) 
                    : new();
                if (extendedMetadata == null)
                {
                    _logger.LogWarning("Failed to deserialize extended metadata for game type {Key}. Skipping.", game.Key);
                    extendedMetadata = new GameTypeExtendedMetadata { GameTypeKey = game.Key };
                }

                var entity = new GameTypeEntity
                {
                    Key = game.Key,
                    DisplayName = game.Value.DisplayName,
                    Description = game.Value.Description,
                    Image = game.Value.Image,
                    ThumbnailUrl = game.Value.ThumbnailUrl,
                    DocumentationUrl = game.Value.DocumentationUrl,
                    IsActive = true,
                    Ports = game.Value.Ports?.Select(p => new PortEntity
                    {
                        Port = (int)p.Port,
                        Protocol = p.Protocol,
                        IsDefaultPort = p.IsDefaultPort
                    }).ToList() ?? new List<PortEntity>(),
                    Volumes = game.Value.Volumes?.Select(v => new VolumeEntity
                    {
                        Source = v.Source,
                        Target = v.Target,
                    }).ToList() ?? new List<VolumeEntity>(),
                    DefaultSettings = game.Value.DefaultSettings?.Select(ds => new DefaultSettingEntity
                    {
                        SettingKey = ds.Key,
                        SettingValue = ds.Value,
                        SettingsMetadata= new SettingMetadataEntity
                        {
                            Description = extendedMetadata.SettingsMetadata.ContainsKey(ds.Key) ? extendedMetadata.SettingsMetadata[ds.Key].Description : null,
                            IsRequired = extendedMetadata.SettingsMetadata.ContainsKey(ds.Key) ? extendedMetadata.SettingsMetadata[ds.Key].IsRequired : false,
                            CannotBeEmpty = extendedMetadata.SettingsMetadata.ContainsKey(ds.Key) ? extendedMetadata.SettingsMetadata[ds.Key].CannotBeEmpty : false,
                            DataType = extendedMetadata.SettingsMetadata.ContainsKey(ds.Key) ? extendedMetadata.SettingsMetadata[ds.Key].DataType : null,
                            Category = extendedMetadata.SettingsMetadata.ContainsKey(ds.Key) ? extendedMetadata.SettingsMetadata[ds.Key].Category : null,
                            DisplayOrder = extendedMetadata.SettingsMetadata.ContainsKey(ds.Key) ? extendedMetadata.SettingsMetadata[ds.Key].DisplayOrder : 0,
                            Placeholder = extendedMetadata.SettingsMetadata.ContainsKey(ds.Key) ? extendedMetadata.SettingsMetadata[ds.Key].Placeholder : null,
                            ValidationPattern = extendedMetadata.SettingsMetadata.ContainsKey(ds.Key) ? extendedMetadata.SettingsMetadata[ds.Key].ValidationPattern : null,
                            ValidationMessage = extendedMetadata.SettingsMetadata.ContainsKey(ds.Key) ? extendedMetadata.SettingsMetadata[ds.Key].ValidationMessage : null,
                            MapsToContainerPort = extendedMetadata.SettingsMetadata.ContainsKey(ds.Key) ? extendedMetadata.SettingsMetadata[ds.Key].MapsToContainerPort : false,
                            LinkedContainerPort = extendedMetadata.SettingsMetadata.ContainsKey(ds.Key) && extendedMetadata.SettingsMetadata[ds.Key].LinkedContainerPort.HasValue
                                ? (int)extendedMetadata.SettingsMetadata[ds.Key].LinkedContainerPort!.Value
                                : null,
                            PortProtocol = extendedMetadata.SettingsMetadata.ContainsKey(ds.Key) ? extendedMetadata.SettingsMetadata[ds.Key].PortProtocol : "",
                            SynchronizedWithSetting = extendedMetadata.SettingsMetadata.ContainsKey(ds.Key) ? extendedMetadata.SettingsMetadata[ds.Key].SynchronizedWithSetting : null,
                            AutoAllocatePort = extendedMetadata.SettingsMetadata.ContainsKey(ds.Key) ? extendedMetadata.SettingsMetadata[ds.Key].AutoAllocatePort : false,
                            ValidateRelatedPortsAvailability = extendedMetadata.SettingsMetadata.ContainsKey(ds.Key) ? extendedMetadata.SettingsMetadata[ds.Key].ValidateRelatedPortsAvailability : false,
                            ListDelimiter = extendedMetadata.SettingsMetadata.ContainsKey(ds.Key) ? extendedMetadata.SettingsMetadata[ds.Key].ListDelimiter : ",",
                            AllowedValuesJson = (extendedMetadata.SettingsMetadata.ContainsKey(ds.Key) && extendedMetadata.SettingsMetadata[ds.Key].AllowedValues != null)
                                ? JsonSerializer.Serialize(extendedMetadata.SettingsMetadata[ds.Key].AllowedValues)
                                : null,
                            ValueMappingsJson = (extendedMetadata.SettingsMetadata.ContainsKey(ds.Key) && extendedMetadata.SettingsMetadata[ds.Key].ValueMappings != null)
                                ? JsonSerializer.Serialize(extendedMetadata.SettingsMetadata[ds.Key].ValueMappings)
                                : null,
                            PortValidation = extendedMetadata.SettingsMetadata.ContainsKey(ds.Key) && extendedMetadata.SettingsMetadata[ds.Key].PortValidation != null
                                ? new PortValidationEntity
                                {
                                    MinPort = (int)extendedMetadata.SettingsMetadata[ds.Key].PortValidation!.MinPort,
                                    MaxPort = (int)extendedMetadata.SettingsMetadata[ds.Key].PortValidation!.MaxPort,
                                    ReservedPortsJson = extendedMetadata.SettingsMetadata[ds.Key].PortValidation!.ReservedPorts != null
                                        ? JsonSerializer.Serialize(extendedMetadata.SettingsMetadata[ds.Key].PortValidation!.ReservedPorts)
                                        : null,
                                    CheckAvailability = extendedMetadata.SettingsMetadata[ds.Key].PortValidation!.CheckAvailability,
                                    IsUserEditable = extendedMetadata.SettingsMetadata[ds.Key].PortValidation!.IsUserEditable,
                                    SuggestedPortsJson = extendedMetadata.SettingsMetadata[ds.Key].PortValidation!.SuggestedPorts != null
                                        ? JsonSerializer.Serialize(extendedMetadata.SettingsMetadata[ds.Key].PortValidation!.SuggestedPorts)
                                        : null,
                                    ValidationMessage = extendedMetadata.SettingsMetadata[ds.Key].PortValidation!.ValidationMessage
                                }
                                : null,
                            PortRelationships = extendedMetadata.SettingsMetadata.ContainsKey(ds.Key) && extendedMetadata.SettingsMetadata[ds.Key].PortRelationships != null
                                ? extendedMetadata.SettingsMetadata[ds.Key].PortRelationships!.Select(pr => new PortRelationshipEntity
                                {
                                    RelationType = (int)pr.RelationType,
                                    TargetContainerPort = (int)pr.TargetContainerPort,
                                    TargetProtocol = pr.TargetProtocol,
                                    OffsetValue = pr.Offset,
                                    FixedValue = pr.FixedValue.HasValue ? (int?)pr.FixedValue.Value : null,
                                    Description = pr.Description,
                                    IsRequired = pr.IsRequired,
                                    DisplayOrder = 0
                                }).ToList()
                                : new List<PortRelationshipEntity>(),

                        }
                    }).ToList() ?? new List<DefaultSettingEntity>(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                _context.GameTypes.Add(entity);

                migrated++;
                _logger.LogInformation("Migrated GameType: {Key}", game.Key);
            }
            if (migrated > 0)
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Migration complete. Migrated {Count} game types from JSON to database.", migrated);
            }
        }
    }
}
