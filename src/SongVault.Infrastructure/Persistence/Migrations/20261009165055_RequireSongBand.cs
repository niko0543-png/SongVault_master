using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SongVault.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequireSongBand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Songs_OwnerId_Title",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Songs");

            migrationBuilder.AlterColumn<Guid>(
                name: "BandId",
                table: "Songs",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Songs_BandId_Title",
                table: "Songs",
                columns: new[] { "BandId", "Title" });

            migrationBuilder.AddForeignKey(
                name: "FK_Songs_Bands_BandId",
                table: "Songs",
                column: "BandId",
                principalTable: "Bands",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Songs_Bands_BandId",
                table: "Songs");

            migrationBuilder.DropIndex(
                name: "IX_Songs_BandId_Title",
                table: "Songs");

            migrationBuilder.AlterColumn<Guid>(
                name: "BandId",
                table: "Songs",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Songs",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            // Recopie le propriétaire d'origine depuis le groupe, avant que BandId ne redevienne facultatif
            migrationBuilder.Sql("""
                UPDATE s SET s.OwnerId = m.UserId
                FROM Songs s JOIN BandMemberships m ON m.BandId = s.BandId AND m.Role = 'Owner';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Songs_OwnerId_Title",
                table: "Songs",
                columns: new[] { "OwnerId", "Title" });
        }
    }
}
