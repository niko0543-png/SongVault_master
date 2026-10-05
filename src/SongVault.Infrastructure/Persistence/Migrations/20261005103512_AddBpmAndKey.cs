using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SongVault.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBpmAndKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Bpm",
                table: "SongVersions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Key",
                table: "SongVersions",
                type: "nvarchar(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SongVersions_Bpm",
                table: "SongVersions",
                sql: "[Bpm] IS NULL OR [Bpm] BETWEEN 20 AND 300");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SongVersions_Bpm",
                table: "SongVersions");

            migrationBuilder.DropColumn(
                name: "Bpm",
                table: "SongVersions");

            migrationBuilder.DropColumn(
                name: "Key",
                table: "SongVersions");
        }
    }
}
