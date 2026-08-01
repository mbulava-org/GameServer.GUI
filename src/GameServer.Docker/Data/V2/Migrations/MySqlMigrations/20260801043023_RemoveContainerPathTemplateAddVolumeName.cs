using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GameServer.Docker.Data.V2.Migrations.MySqlMigrations
{
    /// <inheritdoc />
    public partial class RemoveContainerPathTemplateAddVolumeName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "MountTypeConfigs",
                keyColumn: "Key",
                keyValue: "bind");

            migrationBuilder.DeleteData(
                table: "MountTypeConfigs",
                keyColumn: "Key",
                keyValue: "tmpfs");

            migrationBuilder.DropColumn(
                name: "ContainerPathTemplate",
                table: "MountTypeConfigs");

            migrationBuilder.DropColumn(
                name: "DefaultInitMode",
                table: "MountTypeConfigs");

            migrationBuilder.DropColumn(
                name: "InitMode",
                table: "GameServerVolumes");

            migrationBuilder.DropColumn(
                name: "SeedSourcePath",
                table: "GameServerVolumes");

            migrationBuilder.AddColumn<bool>(
                name: "DefaultEnsureNfsPathExists",
                table: "MountTypeConfigs",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnsureNfsPathExists",
                table: "GameTypeVolumes",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OwnerGidVariable",
                table: "GameTypeVolumes",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerUidVariable",
                table: "GameTypeVolumes",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnsureNfsPathExists",
                table: "GameServerVolumes",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VolumeName",
                table: "GameServerVolumes",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "MountTypeConfigs",
                keyColumn: "Key",
                keyValue: "nfs",
                columns: new[] { "DefaultEnsureNfsPathExists", "Driver" },
                values: new object[] { true, "local" });

            migrationBuilder.UpdateData(
                table: "MountTypeConfigs",
                keyColumn: "Key",
                keyValue: "volume",
                column: "DefaultEnsureNfsPathExists",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultEnsureNfsPathExists",
                table: "MountTypeConfigs");

            migrationBuilder.DropColumn(
                name: "EnsureNfsPathExists",
                table: "GameTypeVolumes");

            migrationBuilder.DropColumn(
                name: "OwnerGidVariable",
                table: "GameTypeVolumes");

            migrationBuilder.DropColumn(
                name: "OwnerUidVariable",
                table: "GameTypeVolumes");

            migrationBuilder.DropColumn(
                name: "EnsureNfsPathExists",
                table: "GameServerVolumes");

            migrationBuilder.DropColumn(
                name: "VolumeName",
                table: "GameServerVolumes");

            migrationBuilder.AddColumn<string>(
                name: "ContainerPathTemplate",
                table: "MountTypeConfigs",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DefaultInitMode",
                table: "MountTypeConfigs",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InitMode",
                table: "GameServerVolumes",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SeedSourcePath",
                table: "GameServerVolumes",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "MountTypeConfigs",
                keyColumn: "Key",
                keyValue: "nfs",
                columns: new[] { "ContainerPathTemplate", "DefaultInitMode", "Driver" },
                values: new object[] { "{Source}", "none", "vieux/sshfs" });

            migrationBuilder.UpdateData(
                table: "MountTypeConfigs",
                keyColumn: "Key",
                keyValue: "volume",
                columns: new[] { "ContainerPathTemplate", "DefaultInitMode" },
                values: new object[] { "{Source}", "none" });

            migrationBuilder.InsertData(
                table: "MountTypeConfigs",
                columns: new[] { "Key", "ContainerPathTemplate", "CreatedAt", "DefaultInitMode", "DefaultOwnerGid", "DefaultOwnerUid", "DefaultPermissions", "DefaultReadOnly", "Description", "DisplayName", "Driver", "DriverOptionsJson", "IsActive", "SourcePathTemplate", "UpdatedAt" },
                values: new object[,]
                {
                    { "bind", "{Source}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "none", null, null, null, false, null, "Bind mount", "local", null, true, "/host/gameservers/{gameTypeKey}/{serverId}/{Source}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "tmpfs", "{Source}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "none", null, null, null, false, null, "tmpfs", "local", null, true, "", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }
    }
}
