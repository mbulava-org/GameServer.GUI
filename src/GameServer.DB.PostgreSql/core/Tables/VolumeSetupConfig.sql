CREATE TABLE IF NOT EXISTS core."VolumeSetupConfig"
(
	"Id" SERIAL PRIMARY KEY,
	"ConfigKey" INTEGER NOT NULL UNIQUE DEFAULT 1,
	"DriverName" VARCHAR(100) NOT NULL DEFAULT 'local',
	"RootStoragePath" VARCHAR(500) NOT NULL DEFAULT '',
	"SubPathFormat" VARCHAR(500) NOT NULL DEFAULT '{gameTypeKey}/{serverId}/{Source}',
	"LocalStoragePath" VARCHAR(500) NULL,
	"DriverOptionsType" VARCHAR(50) NOT NULL DEFAULT 'nfs',
	"DriverOptionsDevice" VARCHAR(500) NOT NULL DEFAULT ':/exported/path',
	"DriverOptionsO" VARCHAR(500) NOT NULL DEFAULT 'addr=host.docker.internal,rw',
	"CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
	"UpdatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS "IX_VolumeSetupConfig_ConfigKey"
	ON core."VolumeSetupConfig" ("ConfigKey");
