using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace GameServer.API.Data.V2.Migrations.MySqlMigrations
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
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ServerId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CpuUsagePercent = table.Column<double>(type: "double", nullable: true),
                    MemoryUsageBytes = table.Column<long>(type: "bigint", nullable: true),
                    MemoryLimitBytes = table.Column<long>(type: "bigint", nullable: true),
                    MemoryUsagePercent = table.Column<double>(type: "double", nullable: true),
                    NetworkRxBytes = table.Column<long>(type: "bigint", nullable: true),
                    NetworkTxBytes = table.Column<long>(type: "bigint", nullable: true),
                    BlockReadBytes = table.Column<long>(type: "bigint", nullable: true),
                    BlockWriteBytes = table.Column<long>(type: "bigint", nullable: true),
                    DesiredReplicas = table.Column<int>(type: "int", nullable: false),
                    RunningReplicas = table.Column<int>(type: "int", nullable: false),
                    ContainerId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameServerResourceUtilizations", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

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
