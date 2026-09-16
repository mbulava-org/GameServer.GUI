using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace GameServer.API.Data.V2.Migrations.MySqlMigrations
{
    /// <inheritdoc />
    public partial class AddBackupsAndGameTypeResources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResourcesJson",
                table: "GameTypeRevisions",
                type: "longtext",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GameServerBackups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    BackupId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    ServerId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    ServerName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    VolumePath = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false),
                    SourceSubPath = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: true),
                    IsDirectory = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FileName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    FilePath = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUsername = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExtensionDaysAdded = table.Column<int>(type: "int", nullable: false),
                    IsSharedWithGroups = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameServerBackups", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_GameServerBackups_BackupId",
                table: "GameServerBackups",
                column: "BackupId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameServerBackups_CreatedByUserId",
                table: "GameServerBackups",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GameServerBackups_ExpiresAt",
                table: "GameServerBackups",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_GameServerBackups_ServerId",
                table: "GameServerBackups",
                column: "ServerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameServerBackups");

            migrationBuilder.DropColumn(
                name: "ResourcesJson",
                table: "GameTypeRevisions");
        }
    }
}
