CREATE TABLE IF NOT EXISTS "core"."GameServerBackups" (
    "Id" SERIAL PRIMARY KEY,
    "BackupId" VARCHAR(64) NOT NULL UNIQUE,
    "ServerId" VARCHAR(128) NOT NULL,
    "ServerName" VARCHAR(256) NOT NULL,
    "VolumePath" VARCHAR(512) NOT NULL,
    "SourceSubPath" VARCHAR(1024) NULL,
    "IsDirectory" BOOLEAN NOT NULL DEFAULT TRUE,
    "FileName" VARCHAR(256) NOT NULL,
    "FilePath" VARCHAR(1024) NOT NULL,
    "FileSizeBytes" BIGINT NOT NULL DEFAULT 0,
    "CreatedByUserId" INT NOT NULL,
    "CreatedByUsername" VARCHAR(128) NOT NULL,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "ExpiresAt" TIMESTAMPTZ NOT NULL,
    "ExtensionDaysAdded" INT NOT NULL DEFAULT 0,
    "IsSharedWithGroups" BOOLEAN NOT NULL DEFAULT FALSE,
    "IsDeleted" BOOLEAN NOT NULL DEFAULT FALSE,
    "DeletedAt" TIMESTAMPTZ NULL
);

CREATE INDEX IF NOT EXISTS "IX_GameServerBackups_ServerId" ON "core"."GameServerBackups" ("ServerId");
CREATE INDEX IF NOT EXISTS "IX_GameServerBackups_CreatedByUserId" ON "core"."GameServerBackups" ("CreatedByUserId");
CREATE INDEX IF NOT EXISTS "IX_GameServerBackups_ExpiresAt" ON "core"."GameServerBackups" ("ExpiresAt");
