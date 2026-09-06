using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.API.Data.V2.Migrations.MySqlMigrations
{
    /// <inheritdoc />
    public partial class MountTypeConfigVolumeNameFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VolumeNameFormat",
                table: "MountTypeConfigs",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "MountTypeConfigs",
                keyColumn: "Key",
                keyValue: "nfs",
                column: "VolumeNameFormat",
                value: "{gameTypeKey}_{serverId}_{Source}");

            migrationBuilder.UpdateData(
                table: "MountTypeConfigs",
                keyColumn: "Key",
                keyValue: "volume",
                column: "VolumeNameFormat",
                value: "{gameTypeKey}_{serverId}_{Source}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VolumeNameFormat",
                table: "MountTypeConfigs");
        }
    }
}
