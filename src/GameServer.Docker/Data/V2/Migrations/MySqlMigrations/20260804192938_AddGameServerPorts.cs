using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace GameServer.Docker.Data.V2.Migrations.MySqlMigrations
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    GameServerId = table.Column<int>(type: "int", nullable: false),
                    ContainerPort = table.Column<int>(type: "int", nullable: false),
                    Protocol = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    PublishedPort = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
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
                })
                .Annotation("MySQL:Charset", "utf8mb4");

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
