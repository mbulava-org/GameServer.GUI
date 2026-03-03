using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GameServer.Docker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTimezoneDataType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SettingsMetadata_DataType",
                table: "SettingsMetadata");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Ports_Protocol",
                table: "Ports");

            migrationBuilder.DeleteData(
                table: "DefaultSettings",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "DefaultSettings",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Ports",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Ports",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "GameTypes",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SettingsMetadata_DataType",
                table: "SettingsMetadata",
                sql: "DataType IS NULL OR DataType IN ('string', 'number', 'boolean', 'enum', 'list', 'port', 'timezone')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Ports_Protocol",
                table: "Ports",
                sql: "Protocol IN ('tcp', 'udp')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SettingsMetadata_DataType",
                table: "SettingsMetadata");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Ports_Protocol",
                table: "Ports");

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

            migrationBuilder.AddCheckConstraint(
                name: "CK_SettingsMetadata_DataType",
                table: "SettingsMetadata",
                sql: "DataType IS NULL OR DataType IN ('string', 'number', 'boolean', 'enum', 'list', 'port')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Ports_Protocol",
                table: "Ports",
                sql: "Protocol IN ('tcp', 'udp', 'tcp/udp')");
        }
    }
}
