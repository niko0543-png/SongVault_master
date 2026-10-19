using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SongVault.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "SongVersions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedById",
                table: "SongVersions",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Songs",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedById",
                table: "Songs",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "SongFiles",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedById",
                table: "SongFiles",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SongVersions_DeletedAt",
                table: "SongVersions",
                column: "DeletedAt",
                filter: "[DeletedAt] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Songs_DeletedAt",
                table: "Songs",
                column: "DeletedAt",
                filter: "[DeletedAt] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SongFiles_DeletedAt",
                table: "SongFiles",
                column: "DeletedAt",
                filter: "[DeletedAt] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SongVersions_DeletedAt",
                table: "SongVersions");

            migrationBuilder.DropIndex(
                name: "IX_Songs_DeletedAt",
                table: "Songs");

            migrationBuilder.DropIndex(
                name: "IX_SongFiles_DeletedAt",
                table: "SongFiles");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "SongVersions");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                table: "SongVersions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "SongFiles");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                table: "SongFiles");
        }
    }
}
