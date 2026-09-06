using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.API.Data.V2.Migrations.SqliteMigrations
{
    /// <inheritdoc />
    public partial class AddReadyLogPatternToGameTypeRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReadyLogPattern",
                table: "GameTypeRevisions",
                type: "TEXT",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReadyLogPattern",
                table: "GameTypeRevisions");
        }
    }
}
