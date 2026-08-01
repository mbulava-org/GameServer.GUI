using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Docker.Data.V2.Migrations.SqliteMigrations
{
    /// <inheritdoc />
    public partial class MountTypeConfigOptionsJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultEnsureNfsPathExists",
                table: "MountTypeConfigs");

            migrationBuilder.DropColumn(
                name: "DefaultOwnerGid",
                table: "MountTypeConfigs");

            migrationBuilder.DropColumn(
                name: "DefaultOwnerUid",
                table: "MountTypeConfigs");

            migrationBuilder.DropColumn(
                name: "DefaultPermissions",
                table: "MountTypeConfigs");

            migrationBuilder.DropColumn(
                name: "DefaultReadOnly",
                table: "MountTypeConfigs");

            migrationBuilder.DropColumn(
                name: "Driver",
                table: "MountTypeConfigs");

            migrationBuilder.DropColumn(
                name: "SourcePathTemplate",
                table: "MountTypeConfigs");

            migrationBuilder.RenameColumn(
                name: "DriverOptionsJson",
                table: "MountTypeConfigs",
                newName: "OptionsJson");

            migrationBuilder.UpdateData(
                table: "MountTypeConfigs",
                keyColumn: "Key",
                keyValue: "nfs",
                column: "OptionsJson",
                value: "{\"Driver\":\"local\",\"DriverOptionsJson\":\"{\\\"type\\\":\\\"nfs\\\",\\\"device\\\":\\\":/exported/path\\\",\\\"o\\\":\\\"addr=host.docker.internal,rw\\\"}\",\"SourcePathTemplate\":\"{gameTypeKey}_{serverId}_{Source}\",\"DefaultReadOnly\":\"false\",\"DefaultEnsureNfsPathExists\":\"true\"}");

            migrationBuilder.UpdateData(
                table: "MountTypeConfigs",
                keyColumn: "Key",
                keyValue: "volume",
                column: "OptionsJson",
                value: "{\"Driver\":\"local\",\"SourcePathTemplate\":\"{gameTypeKey}_{serverId}_{Source}\",\"DefaultReadOnly\":\"false\",\"DefaultEnsureNfsPathExists\":\"false\"}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OptionsJson",
                table: "MountTypeConfigs",
                newName: "DriverOptionsJson");

            migrationBuilder.AddColumn<bool>(
                name: "DefaultEnsureNfsPathExists",
                table: "MountTypeConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DefaultOwnerGid",
                table: "MountTypeConfigs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultOwnerUid",
                table: "MountTypeConfigs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultPermissions",
                table: "MountTypeConfigs",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultReadOnly",
                table: "MountTypeConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Driver",
                table: "MountTypeConfigs",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourcePathTemplate",
                table: "MountTypeConfigs",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "MountTypeConfigs",
                keyColumn: "Key",
                keyValue: "nfs",
                columns: new[] { "DefaultEnsureNfsPathExists", "DefaultOwnerGid", "DefaultOwnerUid", "DefaultPermissions", "DefaultReadOnly", "Driver", "DriverOptionsJson", "SourcePathTemplate" },
                values: new object[] { true, null, null, null, false, "local", "{\"type\":\"nfs\",\"device\":\":/exported/path\",\"o\":\"addr=host.docker.internal,rw\"}", "{gameTypeKey}_{serverId}_{Source}" });

            migrationBuilder.UpdateData(
                table: "MountTypeConfigs",
                keyColumn: "Key",
                keyValue: "volume",
                columns: new[] { "DefaultEnsureNfsPathExists", "DefaultOwnerGid", "DefaultOwnerUid", "DefaultPermissions", "DefaultReadOnly", "Driver", "DriverOptionsJson", "SourcePathTemplate" },
                values: new object[] { false, null, null, null, false, "local", null, "{gameTypeKey}_{serverId}_{Source}" });
        }
    }
}
