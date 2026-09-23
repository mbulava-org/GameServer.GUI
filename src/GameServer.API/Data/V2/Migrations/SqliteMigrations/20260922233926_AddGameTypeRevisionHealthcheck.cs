using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.API.Data.V2.Migrations.SqliteMigrations
{
    /// <inheritdoc />
    public partial class AddGameTypeRevisionHealthcheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HealthcheckJson",
                table: "GameTypeRevisions",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HealthcheckJson",
                table: "GameTypeRevisions");
        }
    }
}
