using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GameServer.Docker.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitalCreate : Migration
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
                    Image = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DocumentationUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DefaultSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    SettingKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SettingValue = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DefaultSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DefaultSettings_GameTypes_GameTypeId",
                        column: x => x.GameTypeId,
                        principalTable: "GameTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExtendedMetadata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    EnableTTY = table.Column<bool>(type: "INTEGER", nullable: false),
                    CustomPropertiesJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtendedMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExtendedMetadata_GameTypes_GameTypeId",
                        column: x => x.GameTypeId,
                        principalTable: "GameTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    Protocol = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    IsDefaultPort = table.Column<bool>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ports", x => x.Id);
                    table.CheckConstraint("CK_Ports_Protocol", "Protocol IN ('tcp', 'udp', 'tcp/udp')");
                    table.CheckConstraint("CK_Ports_Range", "Port >= 1 AND Port <= 65535");
                    table.ForeignKey(
                        name: "FK_Ports_GameTypes_GameTypeId",
                        column: x => x.GameTypeId,
                        principalTable: "GameTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Volumes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Target = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ReadOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Volumes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Volumes_GameTypes_GameTypeId",
                        column: x => x.GameTypeId,
                        principalTable: "GameTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SettingsMetadata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DefaultSettingId = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    IsRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    CannotBeEmpty = table.Column<bool>(type: "INTEGER", nullable: false),
                    DataType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Placeholder = table.Column<string>(type: "TEXT", nullable: true),
                    ValidationPattern = table.Column<string>(type: "TEXT", nullable: true),
                    ValidationMessage = table.Column<string>(type: "TEXT", nullable: true),
                    MapsToContainerPort = table.Column<bool>(type: "INTEGER", nullable: false),
                    LinkedContainerPort = table.Column<int>(type: "INTEGER", nullable: true),
                    PortProtocol = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false, defaultValue: "tcp"),
                    SynchronizedWithSetting = table.Column<string>(type: "TEXT", nullable: true),
                    AutoAllocatePort = table.Column<bool>(type: "INTEGER", nullable: false),
                    ValidateRelatedPortsAvailability = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    ListDelimiter = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false, defaultValue: ","),
                    AllowedValuesJson = table.Column<string>(type: "TEXT", nullable: true),
                    ValueMappingsJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettingsMetadata", x => x.Id);
                    table.CheckConstraint("CK_SettingsMetadata_DataType", "DataType IS NULL OR DataType IN ('string', 'number', 'boolean', 'enum', 'list', 'port')");
                    table.ForeignKey(
                        name: "FK_SettingsMetadata_DefaultSettings_DefaultSettingId",
                        column: x => x.DefaultSettingId,
                        principalTable: "DefaultSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PortRelationships",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SettingMetadataId = table.Column<int>(type: "INTEGER", nullable: false),
                    RelationType = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetContainerPort = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetProtocol = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false, defaultValue: "udp"),
                    OffsetValue = table.Column<int>(type: "INTEGER", nullable: false),
                    FixedValue = table.Column<int>(type: "INTEGER", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    IsRequired = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortRelationships", x => x.Id);
                    table.CheckConstraint("CK_PortRelationships_Protocol", "TargetProtocol IN ('tcp', 'udp')");
                    table.CheckConstraint("CK_PortRelationships_Type", "RelationType IN (0, 1, 2)");
                    table.ForeignKey(
                        name: "FK_PortRelationships_SettingsMetadata_SettingMetadataId",
                        column: x => x.SettingMetadataId,
                        principalTable: "SettingsMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PortValidation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SettingMetadataId = table.Column<int>(type: "INTEGER", nullable: false),
                    MinPort = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1024),
                    MaxPort = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 65535),
                    ReservedPortsJson = table.Column<string>(type: "TEXT", nullable: true),
                    CheckAvailability = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    IsUserEditable = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    SuggestedPortsJson = table.Column<string>(type: "TEXT", nullable: true),
                    ValidationMessage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortValidation", x => x.Id);
                    table.CheckConstraint("CK_PortValidation_Range", "MinPort >= 1 AND MinPort <= MaxPort AND MaxPort <= 65535");
                    table.ForeignKey(
                        name: "FK_PortValidation_SettingsMetadata_SettingMetadataId",
                        column: x => x.SettingMetadataId,
                        principalTable: "SettingsMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "GameTypes",
                columns: new[] { "Id", "CreatedAt", "Description", "DisplayName", "DocumentationUrl", "Image", "IsActive", "Key", "ThumbnailUrl", "UpdatedAt" },
                values: new object[] { 1, new DateTime(2026, 2, 14, 20, 25, 10, 372, DateTimeKind.Utc).AddTicks(6748), "Java Edition Minecraft Server", "Minecraft Server", "https://hub.docker.com/r/itzg/minecraft-server", "itzg/minecraft-server:latest", true, "minecraft", "https://static.wikia.nocookie.net/minecraft_gamepedia/images/2/2d/Plains_Banner.png", new DateTime(2026, 2, 14, 20, 25, 10, 372, DateTimeKind.Utc).AddTicks(6976) });

            migrationBuilder.InsertData(
                table: "DefaultSettings",
                columns: new[] { "Id", "Description", "DisplayOrder", "GameTypeId", "SettingKey", "SettingValue" },
                values: new object[,]
                {
                    { 1, null, 0, 1, "EULA", "TRUE" },
                    { 2, null, 0, 1, "VERSION", "LATEST" }
                });

            migrationBuilder.InsertData(
                table: "Ports",
                columns: new[] { "Id", "Description", "DisplayOrder", "GameTypeId", "IsDefaultPort", "Port", "Protocol" },
                values: new object[,]
                {
                    { 1, "Game Port", 0, 1, true, 25565, "tcp" },
                    { 2, "Query Port", 0, 1, false, 25565, "udp" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DefaultSettings_GameTypeId_SettingKey",
                table: "DefaultSettings",
                columns: new[] { "GameTypeId", "SettingKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DefaultSettings_SettingKey",
                table: "DefaultSettings",
                column: "SettingKey");

            migrationBuilder.CreateIndex(
                name: "IX_ExtendedMetadata_GameTypeId",
                table: "ExtendedMetadata",
                column: "GameTypeId",
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
                name: "IX_PortRelationships_SettingMetadataId",
                table: "PortRelationships",
                column: "SettingMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_Ports_GameTypeId",
                table: "Ports",
                column: "GameTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Ports_IsDefaultPort",
                table: "Ports",
                column: "IsDefaultPort");

            migrationBuilder.CreateIndex(
                name: "IX_PortValidation_SettingMetadataId",
                table: "PortValidation",
                column: "SettingMetadataId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SettingsMetadata_Category",
                table: "SettingsMetadata",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_SettingsMetadata_DefaultSettingId",
                table: "SettingsMetadata",
                column: "DefaultSettingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SettingsMetadata_MapsToContainerPort",
                table: "SettingsMetadata",
                column: "MapsToContainerPort");

            migrationBuilder.CreateIndex(
                name: "IX_Volumes_GameTypeId",
                table: "Volumes",
                column: "GameTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExtendedMetadata");

            migrationBuilder.DropTable(
                name: "PortRelationships");

            migrationBuilder.DropTable(
                name: "Ports");

            migrationBuilder.DropTable(
                name: "PortValidation");

            migrationBuilder.DropTable(
                name: "Volumes");

            migrationBuilder.DropTable(
                name: "SettingsMetadata");

            migrationBuilder.DropTable(
                name: "DefaultSettings");

            migrationBuilder.DropTable(
                name: "GameTypes");
        }
    }
}
