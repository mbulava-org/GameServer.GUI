using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.API.Data.V2.Migrations.SqliteMigrations
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
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GameServerBackups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BackupId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ServerId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ServerName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    VolumePath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    SourceSubPath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    IsDirectory = table.Column<bool>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedByUsername = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ExtensionDaysAdded = table.Column<int>(type: "INTEGER", nullable: false),
                    IsSharedWithGroups = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameServerBackups", x => x.Id);
                });

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
