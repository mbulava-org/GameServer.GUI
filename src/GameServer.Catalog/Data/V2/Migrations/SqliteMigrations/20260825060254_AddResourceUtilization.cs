using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.API.Data.V2.Migrations.SqliteMigrations
{
    /// <inheritdoc />
    public partial class AddResourceUtilization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameServerResourceUtilizations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ServerId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CpuUsagePercent = table.Column<double>(type: "REAL", nullable: true),
                    MemoryUsageBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    MemoryLimitBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    MemoryUsagePercent = table.Column<double>(type: "REAL", nullable: true),
                    NetworkRxBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    NetworkTxBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    BlockReadBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    BlockWriteBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    DesiredReplicas = table.Column<int>(type: "INTEGER", nullable: false),
                    RunningReplicas = table.Column<int>(type: "INTEGER", nullable: false),
                    ContainerId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameServerResourceUtilizations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameServerResourceUtilizations_ServerId",
                table: "GameServerResourceUtilizations",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_GameServerResourceUtilizations_ServerId_Timestamp",
                table: "GameServerResourceUtilizations",
                columns: new[] { "ServerId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameServerResourceUtilizations");
        }
    }
}
