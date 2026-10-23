using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SongVault.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSongRelease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReleaseId",
                table: "Songs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReleasedAt",
                table: "Songs",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Label",
                table: "Releases",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StreamingLinks",
                table: "Releases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");                     // #38 : les sorties existantes n'ont pas encore de lien

            migrationBuilder.AddColumn<string>(
                name: "Upc",
                table: "Releases",
                type: "nvarchar(13)",
                maxLength: 13,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Songs_ReleaseId",
                table: "Songs",
                column: "ReleaseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Songs_Releases_ReleaseId",
                table: "Songs",
                column: "ReleaseId",
                principalTable: "Releases",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Songs_Releases_ReleaseId",
                table: "Songs");

            migrationBuilder.DropIndex(
                name: "IX_Songs_ReleaseId",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "ReleaseId",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "ReleasedAt",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "Label",
                table: "Releases");

            migrationBuilder.DropColumn(
                name: "StreamingLinks",
                table: "Releases");

            migrationBuilder.DropColumn(
                name: "Upc",
                table: "Releases");
        }
    }
}
