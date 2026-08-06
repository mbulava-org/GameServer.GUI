using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Docker.Data.V2.Migrations.MySqlMigrations
{
    /// <inheritdoc />
    public partial class SlimGameServerVolumeSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Driver",
                table: "GameServerVolumes");

            migrationBuilder.DropColumn(
                name: "EnsureNfsPathExists",
                table: "GameServerVolumes");

            migrationBuilder.DropColumn(
                name: "LocalPath",
                table: "GameServerVolumes");

            migrationBuilder.DropColumn(
                name: "OwnerGid",
                table: "GameServerVolumes");

            migrationBuilder.DropColumn(
                name: "OwnerUid",
                table: "GameServerVolumes");

            migrationBuilder.DropColumn(
                name: "Permissions",
                table: "GameServerVolumes");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "GameServerVolumes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Driver",
                table: "GameServerVolumes",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "EnsureNfsPathExists",
                table: "GameServerVolumes",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LocalPath",
                table: "GameServerVolumes",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OwnerGid",
                table: "GameServerVolumes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OwnerUid",
                table: "GameServerVolumes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Permissions",
                table: "GameServerVolumes",
                type: "varchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "GameServerVolumes",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }
    }
}
