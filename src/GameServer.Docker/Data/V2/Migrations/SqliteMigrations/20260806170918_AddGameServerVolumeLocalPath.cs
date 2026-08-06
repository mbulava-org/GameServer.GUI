using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Docker.Data.V2.Migrations.SqliteMigrations
{
    /// <inheritdoc />
    public partial class AddGameServerVolumeLocalPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LocalPath",
                table: "GameServerVolumes",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "MountTypeConfigs",
                keyColumn: "Key",
                keyValue: "nfs",
                column: "OptionsJson",
                value: "{\"Driver\":\"local\",\"NfsOptions\":\"addr=host.docker.internal,rw\",\"NfsRoot\":\"/exported/path\",\"DevicePathFormat\":\"{gameTypeKey}/{serverId}/{Source}\",\"LocalPath\":\"/data/nfs\",\"SourcePathTemplate\":\"{gameTypeKey}_{serverId}_{Source}\",\"DefaultReadOnly\":\"false\",\"DefaultEnsureNfsPathExists\":\"true\"}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocalPath",
                table: "GameServerVolumes");

            migrationBuilder.UpdateData(
                table: "MountTypeConfigs",
                keyColumn: "Key",
                keyValue: "nfs",
                column: "OptionsJson",
                value: "{\"Driver\":\"local\",\"DriverOptionsJson\":\"{\\\"type\\\":\\\"nfs\\\",\\\"device\\\":\\\":/exported/path\\\",\\\"o\\\":\\\"addr=host.docker.internal,rw\\\"}\",\"SourcePathTemplate\":\"{gameTypeKey}_{serverId}_{Source}\",\"DefaultReadOnly\":\"false\",\"DefaultEnsureNfsPathExists\":\"true\"}");
        }
    }
}
