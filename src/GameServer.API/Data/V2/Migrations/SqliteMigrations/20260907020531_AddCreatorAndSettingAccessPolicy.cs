using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.API.Data.V2.Migrations.SqliteMigrations
{
    /// <inheritdoc />
    public partial class AddCreatorAndSettingAccessPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccessPolicy",
                table: "GameServerSettings",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "Group");

            migrationBuilder.AddColumn<string>(
                name: "AllowedUserIdsJson",
                table: "GameServerSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "GameServers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameServers_CreatedByUserId",
                table: "GameServers",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_GameServers_Users_CreatedByUserId",
                table: "GameServers",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GameServers_Users_CreatedByUserId",
                table: "GameServers");

            migrationBuilder.DropIndex(
                name: "IX_GameServers_CreatedByUserId",
                table: "GameServers");

            migrationBuilder.DropColumn(
                name: "AccessPolicy",
                table: "GameServerSettings");

            migrationBuilder.DropColumn(
                name: "AllowedUserIdsJson",
                table: "GameServerSettings");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "GameServers");
        }
    }
}
