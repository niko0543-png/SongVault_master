using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SongVault.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StemName",
                table: "SongFiles",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StemPosition",
                table: "SongFiles",
                type: "int",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SongFiles_Stem",
                table: "SongFiles",
                sql: "([StemName] IS NULL AND [StemPosition] IS NULL) OR ([StemName] IS NOT NULL AND [StemPosition] >= 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SongFiles_Stem",
                table: "SongFiles");

            migrationBuilder.DropColumn(
                name: "StemName",
                table: "SongFiles");

            migrationBuilder.DropColumn(
                name: "StemPosition",
                table: "SongFiles");
        }
    }
}
