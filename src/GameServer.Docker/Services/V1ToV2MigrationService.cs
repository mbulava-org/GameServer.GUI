using GameServer.Docker.Data;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Models.V2;
using Microsoft.EntityFrameworkCore;
using RepositoriesV2 = GameServer.Docker.Repositories.V2;

namespace GameServer.Docker.Services;

/// <summary>
/// Hosted service that runs once at startup to migrate V1 game types and servers
/// into the V2 database schema. Safe to run repeatedly — already-migrated records
/// are skipped.
///
/// Migration rules:
/// - V1 GameTypeEntity  →  V2 GameType  +  a single published GameTypeRevision
///   (version tag "v1-migrated", image taken from V1 Image field).
///   The migrated revision is set as the current revision.
/// - V1 Docker-service-based server (identified by V1 labels) that has no matching
///   V2 GameServer record (matched by ServerId)  →  V2 GameServer record using the
///   current revision of the matched V2 GameType.
///
/// If a V1 server's game type has not been migrated to V2 yet (e.g. because V1 DB
/// is empty or the type is unknown), that server is skipped and logged as a warning.
/// </summary>
public sealed class V1ToV2MigrationService : BackgroundService
{
    private const string MigratedVersionTag = "v1-migrated";

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<V1ToV2MigrationService> _logger;

    public V1ToV2MigrationService(
        IServiceProvider serviceProvider,
        ILogger<V1ToV2MigrationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run after the primary DatabaseInitializationService has finished.
        // A short delay ensures both V1 and V2 databases are ready.
        await Task.Delay(500, stoppingToken);

        _logger.LogInformation("🔄 Starting V1 → V2 data migration check...");

        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();

            var v1Context = scope.ServiceProvider.GetRequiredService<GameServerDbContext>();
            var v2GameTypeRepo = scope.ServiceProvider.GetRequiredService<RepositoriesV2.IGameTypeRepository>();
            var v2ServerRepo = scope.ServiceProvider.GetRequiredService<RepositoriesV2.IGameServerRepository>();
#pragma warning disable CS0618 // IGameServerManager is legacy but needed to enumerate V1 Docker servers
            var v1ServerManager = scope.ServiceProvider.GetService<Interfaces.IGameServerManager>();
#pragma warning restore CS0618

            // ---------------------------------------------------------------
            // Step 1 — Migrate V1 GameTypes → V2
            // ---------------------------------------------------------------
            var migratedGameTypeKeys = await MigrateGameTypesAsync(v1Context, v2GameTypeRepo, stoppingToken);

            // ---------------------------------------------------------------
            // Step 2 — Migrate V1 Docker servers → V2 GameServer records
            // ---------------------------------------------------------------
            if (v1ServerManager is not null)
            {
                await MigrateServersAsync(v1ServerManager, v2GameTypeRepo, v2ServerRepo, stoppingToken);
            }
            else
            {
                _logger.LogWarning("IGameServerManager is not registered — skipping V1 server migration.");
            }

            _logger.LogInformation("✅ V1 → V2 migration check complete. Migrated {Count} game type(s).", migratedGameTypeKeys.Count);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("V1 → V2 migration was cancelled during host shutdown.");
        }
        catch (Exception ex)
        {
            // Non-fatal: log and continue — the app can still operate in V2 mode.
            _logger.LogError(ex, "❌ V1 → V2 migration encountered an error. Some V1 data may not have been migrated.");
        }
    }

    // -----------------------------------------------------------------------
    // Game type migration
    // -----------------------------------------------------------------------

    private async Task<List<string>> MigrateGameTypesAsync(
        GameServerDbContext v1Context,
        RepositoriesV2.IGameTypeRepository v2GameTypeRepo,
        CancellationToken ct)
    {
        var migrated = new List<string>();

        // Load all V1 game types with their related data.
        var v1GameTypes = await v1Context.GameTypes
            .Include(gt => gt.Ports)
            .Include(gt => gt.Volumes)
            .Include(gt => gt.DefaultSettings)
                .ThenInclude(ds => ds.SettingsMetadata)
            .Include(gt => gt.ExtendedMetadata)
            .ToListAsync(ct);

        if (v1GameTypes.Count == 0)
        {
            _logger.LogInformation("V1 database contains no game types — nothing to migrate.");
            return migrated;
        }

        _logger.LogInformation("Found {Count} V1 game type(s) to evaluate.", v1GameTypes.Count);

        foreach (var v1 in v1GameTypes)
        {
            ct.ThrowIfCancellationRequested();

            var existing = await v2GameTypeRepo.GetByKeyAsync(v1.Key);
            if (existing is not null)
            {
                _logger.LogDebug("V2 GameType '{Key}' already exists — skipping.", v1.Key);
                continue;
            }

            _logger.LogInformation("Migrating V1 GameType '{Key}' → V2...", v1.Key);

            try
            {
                var revision = BuildV2Revision(v1);

                var v2GameType = new GameType
                {
                    Key = v1.Key,
                    DisplayName = v1.DisplayName,
                    Description = v1.Description,
                    Type = "docker",
                    ThumbnailUrl = v1.ThumbnailUrl,
                    DocumentationUrl = v1.DocumentationUrl,
                    IsActive = v1.IsActive,
                    // Revisions are added after creation via AddRevisionAsync
                };

                var created = await v2GameTypeRepo.CreateAsync(v2GameType);

                // Add the migrated revision.
                var addedRevision = await v2GameTypeRepo.AddRevisionAsync(created.Key, revision);

                // Mark this revision as current and published.
                await v2GameTypeRepo.SetCurrentRevisionAsync(created.Key, addedRevision.Id);

                migrated.Add(v1.Key);
                _logger.LogInformation("✅ Migrated V1 GameType '{Key}' to V2 (revisionId={RevisionId}).", v1.Key, addedRevision.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to migrate V1 GameType '{Key}'. Skipping.", v1.Key);
            }
        }

        return migrated;
    }

    private static GameTypeRevision BuildV2Revision(Data.GameTypeEntity v1)
    {
        var ports = v1.Ports
            .OrderBy(p => p.DisplayOrder)
            .Select((p, i) => new GameTypePort
            {
                ContainerPort = p.Port,
                Protocol = string.IsNullOrWhiteSpace(p.Protocol) ? "tcp" : p.Protocol,
                AdvertisedPort = p.IsDefaultPort,
                Description = p.Description,
                DisplayOrder = p.DisplayOrder == 0 ? i : p.DisplayOrder,
            })
            .ToList();

        var volumes = v1.Volumes
            .OrderBy(v => v.DisplayOrder)
            .Select((v, i) => new GameTypeVolume
            {
                Source = v.Source ?? string.Empty,
                Description = v.Description,
                DisplayOrder = v.DisplayOrder == 0 ? i : v.DisplayOrder,
                Usage = "data",
            })
            .ToList();

        var settings = v1.DefaultSettings
            .OrderBy(s => s.DisplayOrder)
            .Select((s, i) =>
            {
                GameTypeSettingMetadata? meta = null;
                if (s.SettingsMetadata is not null)
                {
                    meta = new GameTypeSettingMetadata
                    {
                        DataType = s.SettingsMetadata.DataType,
                        Category = s.SettingsMetadata.Category,
                        IsRequired = s.SettingsMetadata.IsRequired,
                        CannotBeEmpty = s.SettingsMetadata.CannotBeEmpty,
                        Placeholder = s.SettingsMetadata.Placeholder,
                        ValidationPattern = s.SettingsMetadata.ValidationPattern,
                        ValidationMessage = s.SettingsMetadata.ValidationMessage,
                        AutoAllocatePort = false, // V1 had no concept of auto-allocate
                        AllowedValuesJson = s.SettingsMetadata.AllowedValuesJson,
                        ValueMappingsJson = s.SettingsMetadata.ValueMappingsJson,
                    };
                }

                return new GameTypeSettingDefinition
                {
                    SettingKey = s.SettingKey,
                    DefaultValue = s.SettingValue,
                    Description = s.Description,
                    DisplayOrder = s.DisplayOrder == 0 ? i : s.DisplayOrder,
                    Metadata = meta,
                };
            })
            .ToList();

        bool enableTTY = v1.ExtendedMetadata?.EnableTTY ?? false;

        return new GameTypeRevision
        {
            VersionTag = MigratedVersionTag,
            ImageReference = v1.Image,
            EnableTTY = enableTTY,
            Notes = $"Auto-migrated from V1 on {DateTime.UtcNow:yyyy-MM-dd}.",
            IsPublished = true,
            Ports = ports,
            Volumes = volumes,
            SettingDefinitions = settings,
        };
    }

    // -----------------------------------------------------------------------
    // Server migration
    // -----------------------------------------------------------------------

#pragma warning disable CS0618 // IGameServerManager is legacy
    private async Task MigrateServersAsync(
        Interfaces.IGameServerManager v1Manager,
        RepositoriesV2.IGameTypeRepository v2GameTypeRepo,
        RepositoriesV2.IGameServerRepository v2ServerRepo,
        CancellationToken ct)
#pragma warning restore CS0618
    {
        List<Models.GameServer> v1Servers;
        try
        {
            v1Servers = await v1Manager.ListServersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not enumerate V1 Docker servers — skipping server migration.");
            return;
        }

        if (v1Servers.Count == 0)
        {
            _logger.LogInformation("No V1 Docker servers found — nothing to migrate.");
            return;
        }

        _logger.LogInformation("Found {Count} V1 server(s) to evaluate.", v1Servers.Count);

        // Build a local cache: gameTypeKey → current V2 revision id
        var revisionIdByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        int migratedCount = 0;

        foreach (var v1Server in v1Servers)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(v1Server.ServerId))
            {
                _logger.LogWarning("V1 server has no ServerId — skipping.");
                continue;
            }

            // Check if V2 record already exists.
            var existing = await v2ServerRepo.GetByServerIdAsync(v1Server.ServerId);
            if (existing is not null)
            {
                _logger.LogDebug("V2 GameServer '{ServerId}' already exists — skipping.", v1Server.ServerId);
                continue;
            }

            // Resolve V2 revision.
            if (!revisionIdByKey.TryGetValue(v1Server.GameType, out var revisionId))
            {
                var v2GameType = await v2GameTypeRepo.GetByKeyAsync(v1Server.GameType);
                if (v2GameType?.CurrentRevisionId is null)
                {
                    _logger.LogWarning(
                        "No V2 GameType with a current revision found for key '{GameType}'. " +
                        "Server '{ServerId}' ('{Name}') will not be migrated.",
                        v1Server.GameType, v1Server.ServerId, v1Server.Name);
                    continue;
                }
                revisionId = v2GameType.CurrentRevisionId.Value;
                revisionIdByKey[v1Server.GameType] = revisionId;
            }

            _logger.LogInformation("Migrating V1 server '{ServerId}' ('{Name}') → V2...", v1Server.ServerId, v1Server.Name);

            try
            {
                var v2Server = new Models.V2.GameServer
                {
                    ServerId = v1Server.ServerId,
                    Name = v1Server.Name,
                    Description = v1Server.Description,
                    GameTypeRevisionId = revisionId,
                    ServiceName = v1Server.ServiceName,
                    Status = v1Server.Status,
                    Settings = v1Server.Settings
                        .Select(kv => new Models.V2.GameServerSetting
                        {
                            SettingKey = kv.Key,
                            Value = kv.Value,
                        })
                        .ToList(),
                };

                await v2ServerRepo.CreateAsync(v2Server);
                migratedCount++;
                _logger.LogInformation("✅ Migrated V1 server '{ServerId}'.", v1Server.ServerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to migrate V1 server '{ServerId}'. Skipping.", v1Server.ServerId);
            }
        }

        _logger.LogInformation("Server migration complete: {Count} server(s) migrated.", migratedCount);
    }
}
