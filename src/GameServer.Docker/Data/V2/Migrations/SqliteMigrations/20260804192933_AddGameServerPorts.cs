using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Docker.Data.V2.Migrations.SqliteMigrations
{
    /// <inheritdoc />
    public partial class AddGameServerPorts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameServerPorts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameServerId = table.Column<int>(type: "INTEGER", nullable: false),
                    ContainerPort = table.Column<int>(type: "INTEGER", nullable: false),
                    Protocol = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    PublishedPort = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameServerPorts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameServerPorts_GameServers_GameServerId",
                        column: x => x.GameServerId,
                        principalTable: "GameServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameServerPorts_GameServerId",
                table: "GameServerPorts",
                column: "GameServerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameServerPorts");
        }
    }
}
