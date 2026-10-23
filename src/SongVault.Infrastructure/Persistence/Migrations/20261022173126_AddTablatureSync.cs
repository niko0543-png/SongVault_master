using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SongVault.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTablatureSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SyncAudioFileId",
                table: "SongFiles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SyncPoints",
                table: "SongFiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SongFiles_Sync",
                table: "SongFiles",
                sql: "([SyncAudioFileId] IS NULL AND [SyncPoints] IS NULL) OR ([SyncAudioFileId] IS NOT NULL AND [SyncPoints] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SongFiles_Sync",
                table: "SongFiles");

            migrationBuilder.DropColumn(
                name: "SyncAudioFileId",
                table: "SongFiles");

            migrationBuilder.DropColumn(
                name: "SyncPoints",
                table: "SongFiles");
        }
    }
}
