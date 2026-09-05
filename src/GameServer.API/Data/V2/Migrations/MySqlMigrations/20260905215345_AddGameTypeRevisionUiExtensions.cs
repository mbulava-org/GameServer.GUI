using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.API.Data.V2.Migrations.MySqlMigrations
{
    /// <inheritdoc />
    public partial class AddGameTypeRevisionUiExtensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UiExtensionsJson",
                table: "GameTypeRevisions",
                type: "longtext",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UiExtensionsJson",
                table: "GameTypeRevisions");
        }
    }
}
