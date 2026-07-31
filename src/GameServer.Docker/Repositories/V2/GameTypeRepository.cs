using System.Text.Json;
using GameServer.Docker.Models.V2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using DataV2 = GameServer.Docker.Data.V2;

namespace GameServer.Docker.Repositories.V2;

public class GameTypeRepository(DataV2.GameServerV2DbContext context, ILogger<GameTypeRepository> logger)
    : IGameTypeRepository
{
    private const string InitialV2MigrationId = "20260404190753_RefactorV2GameTypeTypeAndRevisionImageReference";
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
        // Bring pre-migrations databases up to the InitialCreate baseline and record it in history so
        // MigrateAsync only applies genuinely new migrations instead of trying to recreate existing objects.
        await BaselineExistingDatabaseIfNeededAsync().ConfigureAwait(false);

        var pendingMigrations = (await context.Database.GetPendingMigrationsAsync().ConfigureAwait(false)).ToList();
        if (pendingMigrations.Count == 0)
        {
            logger.LogInformation("No pending V2 database migrations to apply.");
        }
        else
        {
            logger.LogInformation(
                "Applying {Count} pending V2 database migration(s): {Migrations}",
                pendingMigrations.Count,
                string.Join(", ", pendingMigrations));
            await context.Database.MigrateAsync().ConfigureAwait(false);
            logger.LogInformation("V2 database migrations applied successfully.");
        }
    }

    /// <summary>
    /// Detects databases that were created before EF Core migrations were adopted (the old
    /// EnsureCreated + synthetic baseline path) and reconciles them to the InitialCreate baseline
    /// without dropping data, then records the real InitialCreate migration in history so the normal
    /// MigrateAsync flow can take over. Fresh databases are left untouched so MigrateAsync creates them.
    /// </summary>
    private async Task BaselineExistingDatabaseIfNeededAsync()
    {
        var initialMigrationId = context.Database.GetMigrations().FirstOrDefault();
        if (string.IsNullOrEmpty(initialMigrationId))
        {
            throw new InvalidOperationException("No V2 EF Core migrations were found for the active provider.");
        }

        // Fresh database: no schema yet. MigrateAsync will create everything from scratch.
        if (!await TableExistsAsync("GameTypes").ConfigureAwait(false))
        {
            return;
        }

        var historyRepository = context.GetService<IHistoryRepository>();
        var historyExists = await historyRepository.ExistsAsync().ConfigureAwait(false);
        var appliedMigrations = historyExists
            ? (await context.Database.GetAppliedMigrationsAsync().ConfigureAwait(false)).ToList()
            : [];

        // The real baseline migration is already recorded: nothing to reconcile, MigrateAsync handles the rest.
        if (appliedMigrations.Contains(initialMigrationId, StringComparer.Ordinal))
        {
            return;
        }

        logger.LogInformation(
            "Existing pre-migrations V2 schema detected. Reconciling to the '{MigrationId}' baseline before recording migration history...",
            initialMigrationId);

        await ReconcileToBaselineSchemaAsync().ConfigureAwait(false);

        // Ensure the history table exists, drop any synthetic baseline marker, then record the real baseline
        // so MigrateAsync treats InitialCreate as already applied instead of recreating existing objects.
        await context.Database.ExecuteSqlRawAsync(historyRepository.GetCreateIfNotExistsScript()).ConfigureAwait(false);
        await RemoveSyntheticBaselineHistoryAsync().ConfigureAwait(false);
        await context.Database.ExecuteSqlRawAsync(
            historyRepository.GetInsertScript(new HistoryRow(initialMigrationId, context.Model.GetProductVersion())))
            .ConfigureAwait(false);

        logger.LogInformation("Recorded baseline V2 migration history '{MigrationId}' for the existing database.", initialMigrationId);
    }

    private async Task RemoveSyntheticBaselineHistoryAsync()
    {
        // Older builds recorded a synthetic baseline id that does not correspond to a real migration.
        var quotedTable = context.Database.IsSqlite() ? "\"__EFMigrationsHistory\"" : "`__EFMigrationsHistory`";
        var quotedColumn = context.Database.IsSqlite() ? "\"MigrationId\"" : "`MigrationId`";
        await context.Database.ExecuteSqlRawAsync(
            $"DELETE FROM {quotedTable} WHERE {quotedColumn} = {{0}};",
            InitialV2MigrationId)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Idempotently reconciles a pre-migrations database up to the InitialCreate baseline schema,
    /// preserving existing rows. Safe to run repeatedly: every change is guarded by an existence check.
    /// </summary>
    private async Task ReconcileToBaselineSchemaAsync()
    {
        var gameTypesHasLegacyImageReference = await ColumnExistsAsync("GameTypes", "ImageReference").ConfigureAwait(false);
        var gameTypesHasType = await ColumnExistsAsync("GameTypes", "Type").ConfigureAwait(false);
        var revisionsHasImageReference = await ColumnExistsAsync("GameTypeRevisions", "ImageReference").ConfigureAwait(false);

        if (gameTypesHasLegacyImageReference && !revisionsHasImageReference)
        {
            logger.LogInformation("Upgrading legacy V2 schema: moving ImageReference from GameTypes into GameTypeRevisions...");
            await UpgradeLegacySchemaAsync().ConfigureAwait(false);
        }
        else if (gameTypesHasLegacyImageReference && gameTypesHasType)
        {
            logger.LogInformation("Removing stale GameTypes.ImageReference column...");
            await RemoveLegacyGameTypesImageReferenceColumnAsync().ConfigureAwait(false);
        }

        await AddMissingBaselineColumnsAndTablesAsync().ConfigureAwait(false);
        await SeedDefaultMountTypeConfigsAsync().ConfigureAwait(false);
    }

    private async Task AddMissingBaselineColumnsAndTablesAsync()
    {
        if (!await TableExistsAsync("MountTypeConfigs").ConfigureAwait(false))
        {
            logger.LogInformation("Creating missing MountTypeConfigs table...");
            foreach (var statement in GetMountTypeConfigTableStatements())
            {
                await context.Database.ExecuteSqlRawAsync(statement).ConfigureAwait(false);
            }
        }

        foreach (var (table, column, sql) in GetPostBaselineColumnAdditions())
        {
            if (!await TableExistsAsync(table).ConfigureAwait(false))
            {
                continue;
            }

            if (!await ColumnExistsAsync(table, column).ConfigureAwait(false))
            {
                logger.LogInformation("Adding missing {Table}.{Column} column...", table, column);
                await context.Database.ExecuteSqlRawAsync(sql).ConfigureAwait(false);
            }
        }

        foreach (var (table, sql) in GetPostBaselineTableCreations())
        {
            if (!await TableExistsAsync(table).ConfigureAwait(false))
            {
                logger.LogInformation("Creating missing {Table} table...", table);
                await context.Database.ExecuteSqlRawAsync(sql).ConfigureAwait(false);
            }
        }
    }

    private IReadOnlyList<string> GetMountTypeConfigTableStatements() =>
        context.Database.IsSqlite()
            ? GetSqliteMountTypeConfigTableStatements()
            : GetMySqlMountTypeConfigTableStatements();

    private IReadOnlyList<(string Table, string Column, string Sql)> GetPostBaselineColumnAdditions() =>
        context.Database.IsSqlite()
            ? GetSqlitePostBaselineColumnAdditions()
            : GetMySqlPostBaselineColumnAdditions();

    private IReadOnlyList<(string Table, string Sql)> GetPostBaselineTableCreations() =>
        context.Database.IsSqlite()
            ? GetSqlitePostBaselineTableCreations()
            : GetMySqlPostBaselineTableCreations();

    private static IReadOnlyList<(string Table, string Column, string Sql)> GetMySqlPostBaselineColumnAdditions()
    {
        return
        [
            ("GameTypeRevisions", "EnableTTY", "ALTER TABLE `GameTypeRevisions` ADD COLUMN `EnableTTY` tinyint(1) NOT NULL DEFAULT 0;"),
            ("GameTypeRevisions", "Notes", "ALTER TABLE `GameTypeRevisions` ADD COLUMN `Notes` longtext NULL;"),
            ("GameTypeRevisions", "IsPublished", "ALTER TABLE `GameTypeRevisions` ADD COLUMN `IsPublished` tinyint(1) NOT NULL DEFAULT 0;"),
            ("GameTypePorts", "AdvertisedPort", "ALTER TABLE `GameTypePorts` ADD COLUMN `AdvertisedPort` tinyint(1) NOT NULL DEFAULT 0;"),
            ("GameTypePorts", "Description", "ALTER TABLE `GameTypePorts` ADD COLUMN `Description` varchar(500) NULL;"),
            ("GameTypePorts", "DisplayOrder", "ALTER TABLE `GameTypePorts` ADD COLUMN `DisplayOrder` int NOT NULL DEFAULT 0;"),
            ("GameTypeVolumes", "MountType", "ALTER TABLE `GameTypeVolumes` ADD COLUMN `MountType` varchar(50) NULL;"),
            ("GameTypeVolumes", "OwnerUid", "ALTER TABLE `GameTypeVolumes` ADD COLUMN `OwnerUid` int NULL;"),
            ("GameTypeVolumes", "OwnerGid", "ALTER TABLE `GameTypeVolumes` ADD COLUMN `OwnerGid` int NULL;"),
            ("GameTypeVolumes", "Permissions", "ALTER TABLE `GameTypeVolumes` ADD COLUMN `Permissions` varchar(10) NULL;"),
            ("GameTypeVolumes", "ReadOnly", "ALTER TABLE `GameTypeVolumes` ADD COLUMN `ReadOnly` tinyint(1) NOT NULL DEFAULT 0;"),
            ("GameTypeVolumes", "Required", "ALTER TABLE `GameTypeVolumes` ADD COLUMN `Required` tinyint(1) NOT NULL DEFAULT 1;"),
            ("GameTypeSettingDefinitions", "DisplayOrder", "ALTER TABLE `GameTypeSettingDefinitions` ADD COLUMN `DisplayOrder` int NOT NULL DEFAULT 0;"),
            ("GameServers", "ServiceName", "ALTER TABLE `GameServers` ADD COLUMN `ServiceName` varchar(200) NOT NULL DEFAULT '';"),
            ("GameServers", "Status", "ALTER TABLE `GameServers` ADD COLUMN `Status` varchar(50) NOT NULL DEFAULT '';"),
            ("GameServers", "IsDeleted", "ALTER TABLE `GameServers` ADD COLUMN `IsDeleted` tinyint(1) NOT NULL DEFAULT 0;"),
            ("GameServers", "GameTypeRevisionId", "ALTER TABLE `GameServers` ADD COLUMN `GameTypeRevisionId` int NULL;"),
            ("GameServers", "CreatedAt", "ALTER TABLE `GameServers` ADD COLUMN `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6);"),
            ("GameServers", "UpdatedAt", "ALTER TABLE `GameServers` ADD COLUMN `UpdatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6);"),
            ("GameServers", "LastDeployedAt", "ALTER TABLE `GameServers` ADD COLUMN `LastDeployedAt` datetime(6) NULL;"),
            ("GameServers", "LastSeenAt", "ALTER TABLE `GameServers` ADD COLUMN `LastSeenAt` datetime(6) NULL;"),
            ("GameServerVolumes", "CreatedAt", "ALTER TABLE `GameServerVolumes` ADD COLUMN `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6);"),
            ("GameServerVolumes", "IsProvisioned", "ALTER TABLE `GameServerVolumes` ADD COLUMN `IsProvisioned` tinyint(1) NOT NULL DEFAULT 0;")
        ];
    }

    private static IReadOnlyList<(string Table, string Sql)> GetMySqlPostBaselineTableCreations()
    {
        return
        [
            ("GameTypeSettingMetadata", """
                CREATE TABLE IF NOT EXISTS `GameTypeSettingMetadata` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `AllowedValuesJson` longtext NULL,
                    `AutoAllocatePort` tinyint(1) NOT NULL DEFAULT 0,
                    `CannotBeEmpty` tinyint(1) NOT NULL DEFAULT 0,
                    `Category` varchar(100) NULL,
                    `DataType` varchar(50) NULL,
                    `GameTypeSettingDefinitionId` int NOT NULL,
                    `IsRequired` tinyint(1) NOT NULL DEFAULT 0,
                    `Placeholder` longtext NULL,
                    `ValidateRelatedPortsAvailability` tinyint(1) NOT NULL DEFAULT 1,
                    `ValidationMessage` longtext NULL,
                    `ValidationPattern` longtext NULL,
                    `ValueMappingsJson` longtext NULL,
                    PRIMARY KEY (`Id`),
                    UNIQUE KEY `IX_GameTypeSettingMetadata_GameTypeSettingDefinitionId` (`GameTypeSettingDefinitionId`)
                );
                """),
            ("GameTypeSettingPortMappings", """
                CREATE TABLE IF NOT EXISTS `GameTypeSettingPortMappings` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `CalculationValue` int NULL,
                    `DisplayOrder` int NOT NULL DEFAULT 0,
                    `GameTypeSettingMetadataId` int NOT NULL,
                    `IsRequired` tinyint(1) NOT NULL DEFAULT 1,
                    `MappingRole` int NOT NULL,
                    `RelationType` int NOT NULL,
                    `TargetContainerPort` int NOT NULL DEFAULT 0,
                    `TargetProtocol` varchar(10) NOT NULL DEFAULT 'udp',
                    PRIMARY KEY (`Id`),
                    KEY `IX_GameTypeSettingPortMappings_GameTypeSettingMetadataId` (`GameTypeSettingMetadataId`),
                    CONSTRAINT `CK_GameTypeSettingPortMappings_Role` CHECK (`MappingRole` IN (0, 1)),
                    CONSTRAINT `CK_GameTypeSettingPortMappings_Type` CHECK (`RelationType` IN (0, 1, 2, 3)),
                    CONSTRAINT `CK_GameTypeSettingPortMappings_Protocol` CHECK (`TargetProtocol` IN ('tcp', 'udp'))
                );
                """),
            ("GameTypeWebHosts", """
                CREATE TABLE IF NOT EXISTS `GameTypeWebHosts` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `ContainerPort` int NULL,
                    `ContainerPortVariable` varchar(200) NULL,
                    `Description` longtext NULL,
                    `DisplayOrder` int NOT NULL DEFAULT 0,
                    `EnabledWhen` varchar(500) NULL,
                    `GameTypeRevisionId` int NOT NULL,
                    `Name` varchar(200) NOT NULL,
                    `PathSegment` varchar(200) NULL,
                    PRIMARY KEY (`Id`),
                    KEY `IX_GameTypeWebHosts_GameTypeRevisionId` (`GameTypeRevisionId`)
                );
                """),
            ("GameServerVolumes", """
                CREATE TABLE IF NOT EXISTS `GameServerVolumes` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `ContainerPath` varchar(500) NOT NULL,
                    `Driver` varchar(200) NOT NULL,
                    `DriverOptionsJson` longtext NULL,
                    `GameServerId` int NOT NULL,
                    `InitMode` varchar(50) NOT NULL,
                    `MountType` varchar(50) NOT NULL,
                    `OwnerGid` int NULL,
                    `OwnerUid` int NULL,
                    `Permissions` varchar(10) NULL,
                    `ReadOnly` tinyint(1) NOT NULL DEFAULT 0,
                    `Required` tinyint(1) NOT NULL DEFAULT 1,
                    `SeedSourcePath` varchar(500) NULL,
                    `Source` varchar(500) NOT NULL,
                    `Usage` varchar(100) NOT NULL,
                    PRIMARY KEY (`Id`),
                    UNIQUE KEY `IX_GameServerVolumes_GameServerId_ContainerPath` (`GameServerId`, `ContainerPath`),
                    KEY `IX_GameServerVolumes_GameServerId` (`GameServerId`)
                );
                """),
            ("GameServerSettings", """
                CREATE TABLE IF NOT EXISTS `GameServerSettings` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `GameServerId` int NOT NULL,
                    `SettingKey` varchar(200) NOT NULL,
                    `Value` longtext NULL,
                    PRIMARY KEY (`Id`),
                    KEY `IX_GameServerSettings_GameServerId` (`GameServerId`)
                );
                """)
        ];
    }

    private static IReadOnlyList<(string Table, string Column, string Sql)> GetSqlitePostBaselineColumnAdditions()
    {
        // SQLite disallows non-constant defaults (e.g. CURRENT_TIMESTAMP) when adding NOT NULL columns,
        // so a constant epoch default is used for timestamp columns; the application overwrites these on save.
        return
        [
            ("GameTypeRevisions", "EnableTTY", "ALTER TABLE \"GameTypeRevisions\" ADD COLUMN \"EnableTTY\" INTEGER NOT NULL DEFAULT 0;"),
            ("GameTypeRevisions", "Notes", "ALTER TABLE \"GameTypeRevisions\" ADD COLUMN \"Notes\" TEXT NULL;"),
            ("GameTypeRevisions", "IsPublished", "ALTER TABLE \"GameTypeRevisions\" ADD COLUMN \"IsPublished\" INTEGER NOT NULL DEFAULT 0;"),
            ("GameTypePorts", "AdvertisedPort", "ALTER TABLE \"GameTypePorts\" ADD COLUMN \"AdvertisedPort\" INTEGER NOT NULL DEFAULT 0;"),
            ("GameTypePorts", "Description", "ALTER TABLE \"GameTypePorts\" ADD COLUMN \"Description\" TEXT NULL;"),
            ("GameTypePorts", "DisplayOrder", "ALTER TABLE \"GameTypePorts\" ADD COLUMN \"DisplayOrder\" INTEGER NOT NULL DEFAULT 0;"),
            ("GameTypeVolumes", "MountType", "ALTER TABLE \"GameTypeVolumes\" ADD COLUMN \"MountType\" TEXT NULL;"),
            ("GameTypeVolumes", "OwnerUid", "ALTER TABLE \"GameTypeVolumes\" ADD COLUMN \"OwnerUid\" INTEGER NULL;"),
            ("GameTypeVolumes", "OwnerGid", "ALTER TABLE \"GameTypeVolumes\" ADD COLUMN \"OwnerGid\" INTEGER NULL;"),
            ("GameTypeVolumes", "Permissions", "ALTER TABLE \"GameTypeVolumes\" ADD COLUMN \"Permissions\" TEXT NULL;"),
            ("GameTypeVolumes", "ReadOnly", "ALTER TABLE \"GameTypeVolumes\" ADD COLUMN \"ReadOnly\" INTEGER NOT NULL DEFAULT 0;"),
            ("GameTypeVolumes", "Required", "ALTER TABLE \"GameTypeVolumes\" ADD COLUMN \"Required\" INTEGER NOT NULL DEFAULT 1;"),
            ("GameTypeSettingDefinitions", "DisplayOrder", "ALTER TABLE \"GameTypeSettingDefinitions\" ADD COLUMN \"DisplayOrder\" INTEGER NOT NULL DEFAULT 0;"),
            ("GameServers", "ServiceName", "ALTER TABLE \"GameServers\" ADD COLUMN \"ServiceName\" TEXT NOT NULL DEFAULT '';"),
            ("GameServers", "Status", "ALTER TABLE \"GameServers\" ADD COLUMN \"Status\" TEXT NOT NULL DEFAULT '';"),
            ("GameServers", "IsDeleted", "ALTER TABLE \"GameServers\" ADD COLUMN \"IsDeleted\" INTEGER NOT NULL DEFAULT 0;"),
            ("GameServers", "GameTypeRevisionId", "ALTER TABLE \"GameServers\" ADD COLUMN \"GameTypeRevisionId\" INTEGER NULL;"),
            ("GameServers", "CreatedAt", "ALTER TABLE \"GameServers\" ADD COLUMN \"CreatedAt\" TEXT NOT NULL DEFAULT '0001-01-01 00:00:00';"),
            ("GameServers", "UpdatedAt", "ALTER TABLE \"GameServers\" ADD COLUMN \"UpdatedAt\" TEXT NOT NULL DEFAULT '0001-01-01 00:00:00';"),
            ("GameServers", "LastDeployedAt", "ALTER TABLE \"GameServers\" ADD COLUMN \"LastDeployedAt\" TEXT NULL;"),
            ("GameServers", "LastSeenAt", "ALTER TABLE \"GameServers\" ADD COLUMN \"LastSeenAt\" TEXT NULL;"),
            ("GameServerVolumes", "CreatedAt", "ALTER TABLE \"GameServerVolumes\" ADD COLUMN \"CreatedAt\" TEXT NOT NULL DEFAULT '0001-01-01 00:00:00';"),
            ("GameServerVolumes", "IsProvisioned", "ALTER TABLE \"GameServerVolumes\" ADD COLUMN \"IsProvisioned\" INTEGER NOT NULL DEFAULT 0;")
        ];
    }

    private static IReadOnlyList<(string Table, string Sql)> GetSqlitePostBaselineTableCreations()
    {
        return
        [
            ("GameTypeSettingMetadata", """
                CREATE TABLE IF NOT EXISTS "GameTypeSettingMetadata" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_GameTypeSettingMetadata" PRIMARY KEY AUTOINCREMENT,
                    "AllowedValuesJson" TEXT NULL,
                    "AutoAllocatePort" INTEGER NOT NULL DEFAULT 0,
                    "CannotBeEmpty" INTEGER NOT NULL DEFAULT 0,
                    "Category" TEXT NULL,
                    "DataType" TEXT NULL,
                    "GameTypeSettingDefinitionId" INTEGER NOT NULL,
                    "IsRequired" INTEGER NOT NULL DEFAULT 0,
                    "Placeholder" TEXT NULL,
                    "ValidateRelatedPortsAvailability" INTEGER NOT NULL DEFAULT 1,
                    "ValidationMessage" TEXT NULL,
                    "ValidationPattern" TEXT NULL,
                    "ValueMappingsJson" TEXT NULL
                );
                """),
            ("GameTypeSettingMetadataIndex", "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_GameTypeSettingMetadata_GameTypeSettingDefinitionId\" ON \"GameTypeSettingMetadata\" (\"GameTypeSettingDefinitionId\");"),
            ("GameTypeSettingPortMappings", """
                CREATE TABLE IF NOT EXISTS "GameTypeSettingPortMappings" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_GameTypeSettingPortMappings" PRIMARY KEY AUTOINCREMENT,
                    "CalculationValue" INTEGER NULL,
                    "DisplayOrder" INTEGER NOT NULL DEFAULT 0,
                    "GameTypeSettingMetadataId" INTEGER NOT NULL,
                    "IsRequired" INTEGER NOT NULL DEFAULT 1,
                    "MappingRole" INTEGER NOT NULL,
                    "RelationType" INTEGER NOT NULL,
                    "TargetContainerPort" INTEGER NOT NULL DEFAULT 0,
                    "TargetProtocol" TEXT NOT NULL DEFAULT 'udp',
                    CONSTRAINT "CK_GameTypeSettingPortMappings_Role" CHECK ("MappingRole" IN (0, 1)),
                    CONSTRAINT "CK_GameTypeSettingPortMappings_Type" CHECK ("RelationType" IN (0, 1, 2, 3)),
                    CONSTRAINT "CK_GameTypeSettingPortMappings_Protocol" CHECK ("TargetProtocol" IN ('tcp', 'udp'))
                );
                """),
            ("GameTypeSettingPortMappingsIndex", "CREATE INDEX IF NOT EXISTS \"IX_GameTypeSettingPortMappings_GameTypeSettingMetadataId\" ON \"GameTypeSettingPortMappings\" (\"GameTypeSettingMetadataId\");"),
            ("GameTypeWebHosts", """
                CREATE TABLE IF NOT EXISTS "GameTypeWebHosts" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_GameTypeWebHosts" PRIMARY KEY AUTOINCREMENT,
                    "ContainerPort" INTEGER NULL,
                    "ContainerPortVariable" TEXT NULL,
                    "Description" TEXT NULL,
                    "DisplayOrder" INTEGER NOT NULL DEFAULT 0,
                    "EnabledWhen" TEXT NULL,
                    "GameTypeRevisionId" INTEGER NOT NULL,
                    "Name" TEXT NOT NULL,
                    "PathSegment" TEXT NULL
                );
                """),
            ("GameTypeWebHostsIndex", "CREATE INDEX IF NOT EXISTS \"IX_GameTypeWebHosts_GameTypeRevisionId\" ON \"GameTypeWebHosts\" (\"GameTypeRevisionId\");"),
            ("GameServerVolumes", """
                CREATE TABLE IF NOT EXISTS "GameServerVolumes" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_GameServerVolumes" PRIMARY KEY AUTOINCREMENT,
                    "ContainerPath" TEXT NOT NULL,
                    "CreatedAt" TEXT NOT NULL DEFAULT '0001-01-01 00:00:00',
                    "Driver" TEXT NOT NULL,
                    "DriverOptionsJson" TEXT NULL,
                    "GameServerId" INTEGER NOT NULL,
                    "InitMode" TEXT NOT NULL,
                    "IsProvisioned" INTEGER NOT NULL DEFAULT 0,
                    "MountType" TEXT NOT NULL,
                    "OwnerGid" INTEGER NULL,
                    "OwnerUid" INTEGER NULL,
                    "Permissions" TEXT NULL,
                    "ReadOnly" INTEGER NOT NULL DEFAULT 0,
                    "Required" INTEGER NOT NULL DEFAULT 1,
                    "SeedSourcePath" TEXT NULL,
                    "Source" TEXT NOT NULL,
                    "Usage" TEXT NOT NULL
                );
                """),
            ("GameServerVolumesUniqueIndex", "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_GameServerVolumes_GameServerId_ContainerPath\" ON \"GameServerVolumes\" (\"GameServerId\", \"ContainerPath\");"),
            ("GameServerSettings", """
                CREATE TABLE IF NOT EXISTS "GameServerSettings" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_GameServerSettings" PRIMARY KEY AUTOINCREMENT,
                    "GameServerId" INTEGER NOT NULL,
                    "SettingKey" TEXT NOT NULL,
                    "Value" TEXT NULL
                );
                """),
            ("GameServerSettingsIndex", "CREATE INDEX IF NOT EXISTS \"IX_GameServerSettings_GameServerId\" ON \"GameServerSettings\" (\"GameServerId\");")
        ];
    }

    private static IReadOnlyList<string> GetSqliteMountTypeConfigTableStatements()
    {
        return
        [
            """
            CREATE TABLE IF NOT EXISTS "MountTypeConfigs" (
                "Key" TEXT NOT NULL CONSTRAINT "PK_MountTypeConfigs" PRIMARY KEY,
                "DisplayName" TEXT NOT NULL,
                "Description" TEXT NULL,
                "Driver" TEXT NOT NULL,
                "DriverOptionsJson" TEXT NULL,
                "SourcePathTemplate" TEXT NOT NULL,
                "ContainerPathTemplate" TEXT NOT NULL,
                "DefaultReadOnly" INTEGER NOT NULL,
                "DefaultInitMode" TEXT NOT NULL,
                "DefaultOwnerUid" INTEGER NULL,
                "DefaultOwnerGid" INTEGER NULL,
                "DefaultPermissions" TEXT NULL,
                "IsActive" INTEGER NOT NULL,
                "CreatedAt" TEXT NOT NULL DEFAULT '0001-01-01 00:00:00',
                "UpdatedAt" TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'
            );
            """
        ];
    }

    private async Task SeedDefaultMountTypeConfigsAsync()
    {
        // Ensure seeded data matches GameServerV2DbContext.HasData for the four built-in mount types.
        var defaultConfigs = new (string Key, string DisplayName, string Driver, string? DriverOptionsJson, string SourcePathTemplate, string ContainerPathTemplate, bool DefaultReadOnly, string DefaultInitMode, bool IsActive)[]
        {
            ("volume", "Docker volume", "local", null, "{gameTypeKey}_{serverId}_{Source}", "{Source}", false, "none", true),
            ("bind", "Bind mount", "local", null, "/host/gameservers/{gameTypeKey}/{serverId}/{Source}", "{Source}", false, "none", true),
            ("tmpfs", "tmpfs", "local", null, string.Empty, "{Source}", false, "none", true),
            ("nfs", "NFS volume", "vieux/sshfs", "{\"type\":\"nfs\",\"device\":\":/exported/path\",\"o\":\"addr=host.docker.internal,rw\"}", "{gameTypeKey}_{serverId}_{Source}", "{Source}", false, "none", true)
        };

        foreach (var config in defaultConfigs)
        {
            if (!await context.MountTypeConfigs.AnyAsync(m => m.Key == config.Key).ConfigureAwait(false))
            {
                logger.LogInformation("Seeding default mount type config '{MountTypeKey}'...", config.Key);
                context.MountTypeConfigs.Add(new DataV2.MountTypeConfigEntity
                {
                    Key = config.Key,
                    DisplayName = config.DisplayName,
                    Driver = config.Driver,
                    DriverOptionsJson = config.DriverOptionsJson,
                    SourcePathTemplate = config.SourcePathTemplate,
                    ContainerPathTemplate = config.ContainerPathTemplate,
                    DefaultReadOnly = config.DefaultReadOnly,
                    DefaultInitMode = config.DefaultInitMode,
                    IsActive = config.IsActive,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    private static IReadOnlyList<string> GetMySqlMountTypeConfigTableStatements()
    {
        return
        [
            """
            CREATE TABLE IF NOT EXISTS `MountTypeConfigs` (
                `Key` varchar(50) NOT NULL,
                `DisplayName` varchar(200) NOT NULL,
                `Description` longtext NULL,
                `Driver` varchar(200) NOT NULL,
                `DriverOptionsJson` longtext NULL,
                `SourcePathTemplate` varchar(500) NOT NULL,
                `ContainerPathTemplate` varchar(500) NOT NULL,
                `DefaultReadOnly` tinyint(1) NOT NULL,
                `DefaultInitMode` varchar(50) NOT NULL,
                `DefaultOwnerUid` int NULL,
                `DefaultOwnerGid` int NULL,
                `DefaultPermissions` varchar(10) NULL,
                `IsActive` tinyint(1) NOT NULL,
                `CreatedAt` datetime(6) NOT NULL,
                `UpdatedAt` datetime(6) NOT NULL,
                PRIMARY KEY (`Key`)
            );
            """
        ];
    }

    private async Task UpgradeLegacySchemaAsync()
    {
        if (context.Database.IsSqlite())
        {
            foreach (var statement in GetSqliteLegacyUpgradeStatements())
            {
                await context.Database.ExecuteSqlRawAsync(statement).ConfigureAwait(false);
            }

            return;
        }

        if (context.Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true)
        {
            foreach (var statement in GetMySqlLegacyUpgradeStatements())
            {
                await context.Database.ExecuteSqlRawAsync(statement).ConfigureAwait(false);
            }

            return;
        }

        throw new NotSupportedException($"Unsupported V2 provider '{context.Database.ProviderName}' for legacy schema upgrade.");
    }

    private async Task RemoveLegacyGameTypesImageReferenceColumnAsync()
    {
        if (context.Database.IsSqlite())
        {
            foreach (var statement in GetSqliteLegacyImageReferenceRemovalStatements())
            {
                await context.Database.ExecuteSqlRawAsync(statement).ConfigureAwait(false);
            }

            return;
        }

        if (context.Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true)
        {
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE `GameTypes` DROP COLUMN `ImageReference`;").ConfigureAwait(false);
            return;
        }

        throw new NotSupportedException($"Unsupported V2 provider '{context.Database.ProviderName}' for legacy ImageReference cleanup.");
    }

    private static IReadOnlyList<string> GetSqliteLegacyUpgradeStatements()
    {
        return
        [
            "PRAGMA foreign_keys=OFF;",
            "ALTER TABLE \"GameTypeRevisions\" ADD COLUMN \"ImageReference\" TEXT NOT NULL DEFAULT '';",
            "UPDATE \"GameTypeRevisions\" SET \"ImageReference\" = COALESCE((SELECT \"ImageReference\" FROM \"GameTypes\" WHERE \"GameTypes\".\"Id\" = \"GameTypeRevisions\".\"GameTypeId\"), '') WHERE \"ImageReference\" = '';",
            "DROP INDEX IF EXISTS \"IX_GameTypeRevisions_GameTypeId_VersionTag\";",
            "CREATE UNIQUE INDEX \"IX_GameTypeRevisions_GameTypeId_ImageReference_VersionTag\" ON \"GameTypeRevisions\" (\"GameTypeId\", \"ImageReference\", \"VersionTag\");",
            "CREATE TABLE \"__GameTypes_Upgrade\" (\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_GameTypes\" PRIMARY KEY AUTOINCREMENT, \"Key\" TEXT NOT NULL, \"DisplayName\" TEXT NOT NULL, \"Description\" TEXT NULL, \"Type\" TEXT NOT NULL DEFAULT 'docker', \"ThumbnailUrl\" TEXT NULL, \"DocumentationUrl\" TEXT NULL, \"IsActive\" INTEGER NOT NULL, \"CurrentRevisionId\" INTEGER NULL, \"CreatedAt\" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, \"UpdatedAt\" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);",
            "INSERT INTO \"__GameTypes_Upgrade\" (\"Id\", \"Key\", \"DisplayName\", \"Description\", \"Type\", \"ThumbnailUrl\", \"DocumentationUrl\", \"IsActive\", \"CurrentRevisionId\", \"CreatedAt\", \"UpdatedAt\") SELECT \"Id\", \"Key\", \"DisplayName\", \"Description\", 'docker', \"ThumbnailUrl\", \"DocumentationUrl\", \"IsActive\", \"CurrentRevisionId\", \"CreatedAt\", \"UpdatedAt\" FROM \"GameTypes\";",
            "DROP TABLE \"GameTypes\";",
            "ALTER TABLE \"__GameTypes_Upgrade\" RENAME TO \"GameTypes\";",
            "CREATE INDEX \"IX_GameTypes_IsActive\" ON \"GameTypes\" (\"IsActive\");",
            "CREATE UNIQUE INDEX \"IX_GameTypes_Key\" ON \"GameTypes\" (\"Key\");",
            "PRAGMA foreign_keys=ON;"
        ];
    }

    private static IReadOnlyList<string> GetSqliteLegacyImageReferenceRemovalStatements()
    {
        return
        [
            "PRAGMA foreign_keys=OFF;",
            "CREATE TABLE \"__GameTypes_Repair\" (\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_GameTypes\" PRIMARY KEY AUTOINCREMENT, \"Key\" TEXT NOT NULL, \"DisplayName\" TEXT NOT NULL, \"Description\" TEXT NULL, \"Type\" TEXT NOT NULL DEFAULT 'docker', \"ThumbnailUrl\" TEXT NULL, \"DocumentationUrl\" TEXT NULL, \"IsActive\" INTEGER NOT NULL, \"CurrentRevisionId\" INTEGER NULL, \"CreatedAt\" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, \"UpdatedAt\" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);",
            "INSERT INTO \"__GameTypes_Repair\" (\"Id\", \"Key\", \"DisplayName\", \"Description\", \"Type\", \"ThumbnailUrl\", \"DocumentationUrl\", \"IsActive\", \"CurrentRevisionId\", \"CreatedAt\", \"UpdatedAt\") SELECT \"Id\", \"Key\", \"DisplayName\", \"Description\", \"Type\", \"ThumbnailUrl\", \"DocumentationUrl\", \"IsActive\", \"CurrentRevisionId\", \"CreatedAt\", \"UpdatedAt\" FROM \"GameTypes\";",
            "DROP TABLE \"GameTypes\";",
            "ALTER TABLE \"__GameTypes_Repair\" RENAME TO \"GameTypes\";",
            "CREATE INDEX \"IX_GameTypes_IsActive\" ON \"GameTypes\" (\"IsActive\");",
            "CREATE UNIQUE INDEX \"IX_GameTypes_Key\" ON \"GameTypes\" (\"Key\");",
            "PRAGMA foreign_keys=ON;"
        ];
    }

    private static IReadOnlyList<string> GetMySqlLegacyUpgradeStatements()
    {
        return
        [
            "ALTER TABLE `GameTypeRevisions` ADD COLUMN `ImageReference` varchar(500) NOT NULL DEFAULT '';",
            "UPDATE `GameTypeRevisions` r INNER JOIN `GameTypes` g ON g.`Id` = r.`GameTypeId` SET r.`ImageReference` = g.`ImageReference` WHERE r.`ImageReference` = '';",
            "ALTER TABLE `GameTypes` ADD COLUMN `Type` varchar(50) NOT NULL DEFAULT 'docker';",
            "DROP INDEX `IX_GameTypeRevisions_GameTypeId_VersionTag` ON `GameTypeRevisions`;",
            "CREATE UNIQUE INDEX `IX_GameTypeRevisions_GameTypeId_ImageReference_VersionTag` ON `GameTypeRevisions` (`GameTypeId`, `ImageReference`, `VersionTag`);",
            "ALTER TABLE `GameTypes` DROP COLUMN `ImageReference`;"
        ];
    }

    private async Task<bool> TableExistsAsync(string tableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        return context.Database.IsSqlite()
            ? await SqliteObjectExistsAsync("table", tableName).ConfigureAwait(false)
            : await MySqlTableExistsAsync(tableName).ConfigureAwait(false);
    }

    private async Task<bool> ColumnExistsAsync(string tableName, string columnName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);

        if (context.Database.IsSqlite())
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{tableName.Replace("\"", "\"\"")}\");";

            if (command.Connection?.State != System.Data.ConnectionState.Open)
            {
                await command.Connection!.OpenAsync().ConfigureAwait(false);
            }

            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        return await MySqlColumnExistsAsync(tableName, columnName).ConfigureAwait(false);
    }

    private async Task<bool> SqliteObjectExistsAsync(string objectType, string objectName)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = $type AND name = $name LIMIT 1;";

        var typeParameter = command.CreateParameter();
        typeParameter.ParameterName = "$type";
        typeParameter.Value = objectType;
        command.Parameters.Add(typeParameter);

        var nameParameter = command.CreateParameter();
        nameParameter.ParameterName = "$name";
        nameParameter.Value = objectName;
        command.Parameters.Add(nameParameter);

        if (command.Connection?.State != System.Data.ConnectionState.Open)
        {
            await command.Connection!.OpenAsync().ConfigureAwait(false);
        }

        return await command.ExecuteScalarAsync().ConfigureAwait(false) is not null;
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

    private async Task<bool> MySqlTableExistsAsync(string tableName)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT 1 FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = @tableName LIMIT 1;";

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

    private async Task<bool> MySqlColumnExistsAsync(string tableName, string columnName)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT 1 FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = @tableName AND column_name = @columnName LIMIT 1;";

        var tableParameter = command.CreateParameter();
        tableParameter.ParameterName = "@tableName";
        tableParameter.Value = tableName;
        command.Parameters.Add(tableParameter);

        var columnParameter = command.CreateParameter();
        columnParameter.ParameterName = "@columnName";
        columnParameter.Value = columnName;
        command.Parameters.Add(columnParameter);

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
            Usage = x.Usage
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
                Permissions = x.Permissions,
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
                Permissions = x.Permissions,
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
