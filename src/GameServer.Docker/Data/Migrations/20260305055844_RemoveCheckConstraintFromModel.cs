using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Docker.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCheckConstraintFromModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SettingsMetadata_DataType",
                table: "SettingsMetadata");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_SettingsMetadata_DataType",
                table: "SettingsMetadata",
                sql: "DataType IS NULL OR DataType IN ('string', 'number', 'boolean', 'enum', 'list', 'port', 'timezone')");
        }
    }
}
