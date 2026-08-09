CREATE TABLE IF NOT EXISTS core."MountTypeConfigs"
(
	"Key" VARCHAR(50) PRIMARY KEY,
	"DisplayName" VARCHAR(200) NOT NULL,
	"Description" TEXT NULL,
	"Driver" VARCHAR(200) NULL,
	"DriverOptionsJson" TEXT NULL,
	"SourcePathTemplate" VARCHAR(500) NULL,
	"ContainerPathTemplate" VARCHAR(500) NULL,
	"DefaultReadOnly" BOOLEAN NOT NULL DEFAULT FALSE,
	"DefaultInitMode" VARCHAR(50) NOT NULL DEFAULT 'none',
	"DefaultOwnerUid" INTEGER NULL,
	"DefaultOwnerGid" INTEGER NULL,
	"DefaultPermissions" VARCHAR(10) NULL,
	"IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
	"CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
	"UpdatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS "IX_MountTypeConfigs_IsActive"
	ON core."MountTypeConfigs" ("IsActive");

-- Migrate data from the legacy VolumeSetupConfig singleton table if it exists.
-- This inserts a default entry for the 'volume' mount type using the old global values.
INSERT INTO core."MountTypeConfigs" (
	"Key",
	"DisplayName",
	"Description",
	"Driver",
	"SourcePathTemplate",
	"ContainerPathTemplate",
	"DefaultReadOnly",
	"DefaultInitMode",
	"IsActive",
	"CreatedAt",
	"UpdatedAt")
SELECT
	'volume',
	'Docker volume',
	'Migrated from legacy VolumeSetupConfig',
	"DriverName",
	"SubPathFormat",
	'{Source}',
	FALSE,
	'none',
	TRUE,
	CURRENT_TIMESTAMP,
	CURRENT_TIMESTAMP
FROM core."VolumeSetupConfig"
WHERE NOT EXISTS (SELECT 1 FROM core."MountTypeConfigs" WHERE "Key" = 'volume')
LIMIT 1;

-- Seed other known defaults if they do not already exist.
INSERT INTO core."MountTypeConfigs" ("Key", "DisplayName", "Driver", "SourcePathTemplate", "ContainerPathTemplate", "DefaultInitMode", "IsActive", "CreatedAt", "UpdatedAt")
VALUES
	('bind', 'Bind mount', 'local', '/host/gameservers/{gameTypeKey}/{serverId}/{Source}', '{Source}', 'none', TRUE, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
	('tmpfs', 'tmpfs', 'local', '', '{Source}', 'none', TRUE, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
	('nfs', 'NFS volume', 'vieux/sshfs', '{gameTypeKey}_{serverId}_{Source}', '{Source}', 'none', TRUE, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
ON CONFLICT ("Key") DO NOTHING;
