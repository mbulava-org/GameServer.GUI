using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GameServer.API.Data.V2.Migrations.SqliteMigrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DocumentationUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CurrentRevisionId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MountTypeConfigs",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Driver = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DriverOptionsJson = table.Column<string>(type: "TEXT", nullable: true),
                    SourcePathTemplate = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ContainerPathTemplate = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    DefaultReadOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                    DefaultInitMode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DefaultOwnerUid = table.Column<int>(type: "INTEGER", nullable: true),
                    DefaultOwnerGid = table.Column<int>(type: "INTEGER", nullable: true),
                    DefaultPermissions = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MountTypeConfigs", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "GameTypeRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    VersionTag = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ImageReference = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ImageDigest = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    EnableTTY = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsPublished = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameTypeRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameTypeRevisions_GameTypes_GameTypeId",
                        column: x => x.GameTypeId,
                        principalTable: "GameTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameServers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ServerId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    GameTypeRevisionId = table.Column<int>(type: "INTEGER", nullable: false),
                    ServiceName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastDeployedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameServers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameServers_GameTypeRevisions_GameTypeRevisionId",
                        column: x => x.GameTypeRevisionId,
                        principalTable: "GameTypeRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GameTypePorts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameTypeRevisionId = table.Column<int>(type: "INTEGER", nullable: false),
                    ContainerPort = table.Column<int>(type: "INTEGER", nullable: false),
                    Protocol = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    AdvertisedPort = table.Column<bool>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameTypePorts", x => x.Id);
                    table.CheckConstraint("CK_GameTypePorts_Protocol", "Protocol IN ('tcp', 'udp')");
                    table.CheckConstraint("CK_GameTypePorts_Range", "ContainerPort >= 1 AND ContainerPort <= 65535");
                    table.ForeignKey(
                        name: "FK_GameTypePorts_GameTypeRevisions_GameTypeRevisionId",
                        column: x => x.GameTypeRevisionId,
                        principalTable: "GameTypeRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameTypeSettingDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameTypeRevisionId = table.Column<int>(type: "INTEGER", nullable: false),
                    SettingKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DefaultValue = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameTypeSettingDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameTypeSettingDefinitions_GameTypeRevisions_GameTypeRevisionId",
                        column: x => x.GameTypeRevisionId,
                        principalTable: "GameTypeRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameTypeVolumes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameTypeRevisionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Usage = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    MountType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ReadOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                    OwnerUid = table.Column<int>(type: "INTEGER", nullable: true),
                    OwnerGid = table.Column<int>(type: "INTEGER", nullable: true),
                    Permissions = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Required = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameTypeVolumes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameTypeVolumes_GameTypeRevisions_GameTypeRevisionId",
                        column: x => x.GameTypeRevisionId,
                        principalTable: "GameTypeRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameTypeVolumes_MountTypeConfigs_MountType",
                        column: x => x.MountType,
                        principalTable: "MountTypeConfigs",
                        principalColumn: "Key",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "GameTypeWebHosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameTypeRevisionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    PathSegment = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ContainerPort = table.Column<int>(type: "INTEGER", nullable: true),
                    ContainerPortVariable = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    EnabledWhen = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameTypeWebHosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameTypeWebHosts_GameTypeRevisions_GameTypeRevisionId",
                        column: x => x.GameTypeRevisionId,
                        principalTable: "GameTypeRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameServerSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameServerId = table.Column<int>(type: "INTEGER", nullable: false),
                    SettingKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameServerSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameServerSettings_GameServers_GameServerId",
                        column: x => x.GameServerId,
                        principalTable: "GameServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameServerVolumes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameServerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Usage = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ContainerPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    MountType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ReadOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                    Driver = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DriverOptionsJson = table.Column<string>(type: "TEXT", nullable: true),
                    OwnerUid = table.Column<int>(type: "INTEGER", nullable: true),
                    OwnerGid = table.Column<int>(type: "INTEGER", nullable: true),
                    Permissions = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    InitMode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SeedSourcePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsProvisioned = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameServerVolumes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameServerVolumes_GameServers_GameServerId",
                        column: x => x.GameServerId,
                        principalTable: "GameServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameTypeSettingMetadata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameTypeSettingDefinitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    DataType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    CannotBeEmpty = table.Column<bool>(type: "INTEGER", nullable: false),
                    Placeholder = table.Column<string>(type: "TEXT", nullable: true),
                    ValidationPattern = table.Column<string>(type: "TEXT", nullable: true),
                    ValidationMessage = table.Column<string>(type: "TEXT", nullable: true),
                    AutoAllocatePort = table.Column<bool>(type: "INTEGER", nullable: false),
                    ValidateRelatedPortsAvailability = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowedValuesJson = table.Column<string>(type: "TEXT", nullable: true),
                    ValueMappingsJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameTypeSettingMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameTypeSettingMetadata_GameTypeSettingDefinitions_GameTypeSettingDefinitionId",
                        column: x => x.GameTypeSettingDefinitionId,
                        principalTable: "GameTypeSettingDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameTypeSettingPortMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameTypeSettingMetadataId = table.Column<int>(type: "INTEGER", nullable: false),
                    MappingRole = table.Column<int>(type: "INTEGER", nullable: false),
                    RelationType = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetContainerPort = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetProtocol = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false, defaultValue: "udp"),
                    CalculationValue = table.Column<int>(type: "INTEGER", nullable: true),
                    IsRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameTypeSettingPortMappings", x => x.Id);
                    table.CheckConstraint("CK_GameTypeSettingPortMappings_Protocol", "TargetProtocol IN ('tcp', 'udp')");
                    table.CheckConstraint("CK_GameTypeSettingPortMappings_Role", "MappingRole IN (0, 1)");
                    table.CheckConstraint("CK_GameTypeSettingPortMappings_Type", "RelationType IN (0, 1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_GameTypeSettingPortMappings_GameTypeSettingMetadata_GameTypeSettingMetadataId",
                        column: x => x.GameTypeSettingMetadataId,
                        principalTable: "GameTypeSettingMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "MountTypeConfigs",
                columns: new[] { "Key", "ContainerPathTemplate", "CreatedAt", "DefaultInitMode", "DefaultOwnerGid", "DefaultOwnerUid", "DefaultPermissions", "DefaultReadOnly", "Description", "DisplayName", "Driver", "DriverOptionsJson", "IsActive", "SourcePathTemplate", "UpdatedAt" },
                values: new object[,]
                {
                    { "bind", "{Source}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "none", null, null, null, false, null, "Bind mount", "local", null, true, "/host/gameservers/{gameTypeKey}/{serverId}/{Source}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "nfs", "{Source}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "none", null, null, null, false, null, "NFS volume", "vieux/sshfs", "{\"type\":\"nfs\",\"device\":\":/exported/path\",\"o\":\"addr=host.docker.internal,rw\"}", true, "{gameTypeKey}_{serverId}_{Source}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "tmpfs", "{Source}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "none", null, null, null, false, null, "tmpfs", "local", null, true, "", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "volume", "{Source}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "none", null, null, null, false, null, "Docker volume", "local", null, true, "{gameTypeKey}_{serverId}_{Source}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameServers_GameTypeRevisionId",
                table: "GameServers",
                column: "GameTypeRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_GameServers_IsDeleted",
                table: "GameServers",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_GameServers_ServerId",
                table: "GameServers",
                column: "ServerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameServerSettings_GameServerId_SettingKey",
                table: "GameServerSettings",
                columns: new[] { "GameServerId", "SettingKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameServerVolumes_GameServerId",
                table: "GameServerVolumes",
                column: "GameServerId");

            migrationBuilder.CreateIndex(
                name: "IX_GameServerVolumes_GameServerId_ContainerPath",
                table: "GameServerVolumes",
                columns: new[] { "GameServerId", "ContainerPath" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameTypePorts_GameTypeRevisionId",
                table: "GameTypePorts",
                column: "GameTypeRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_GameTypePorts_GameTypeRevisionId_AdvertisedPort",
                table: "GameTypePorts",
                columns: new[] { "GameTypeRevisionId", "AdvertisedPort" });

            migrationBuilder.CreateIndex(
                name: "IX_GameTypeRevisions_GameTypeId_ImageReference_VersionTag",
                table: "GameTypeRevisions",
                columns: new[] { "GameTypeId", "ImageReference", "VersionTag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameTypes_IsActive",
                table: "GameTypes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_GameTypes_Key",
                table: "GameTypes",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameTypeSettingDefinitions_GameTypeRevisionId_SettingKey",
                table: "GameTypeSettingDefinitions",
                columns: new[] { "GameTypeRevisionId", "SettingKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameTypeSettingMetadata_GameTypeSettingDefinitionId",
                table: "GameTypeSettingMetadata",
                column: "GameTypeSettingDefinitionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameTypeSettingPortMappings_GameTypeSettingMetadataId",
                table: "GameTypeSettingPortMappings",
                column: "GameTypeSettingMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_GameTypeVolumes_GameTypeRevisionId",
                table: "GameTypeVolumes",
                column: "GameTypeRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_GameTypeVolumes_MountType",
                table: "GameTypeVolumes",
                column: "MountType");

            migrationBuilder.CreateIndex(
                name: "IX_GameTypeWebHosts_GameTypeRevisionId",
                table: "GameTypeWebHosts",
                column: "GameTypeRevisionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameServerSettings");

            migrationBuilder.DropTable(
                name: "GameServerVolumes");

            migrationBuilder.DropTable(
                name: "GameTypePorts");

            migrationBuilder.DropTable(
                name: "GameTypeSettingPortMappings");

            migrationBuilder.DropTable(
                name: "GameTypeVolumes");

            migrationBuilder.DropTable(
                name: "GameTypeWebHosts");

            migrationBuilder.DropTable(
                name: "GameServers");

            migrationBuilder.DropTable(
                name: "GameTypeSettingMetadata");

            migrationBuilder.DropTable(
                name: "MountTypeConfigs");

            migrationBuilder.DropTable(
                name: "GameTypeSettingDefinitions");

            migrationBuilder.DropTable(
                name: "GameTypeRevisions");

            migrationBuilder.DropTable(
                name: "GameTypes");
        }
    }
}
