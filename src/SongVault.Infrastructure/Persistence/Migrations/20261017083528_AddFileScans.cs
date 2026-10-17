using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SongVault.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFileScans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileScans",
                columns: table => new
                {
                    SongFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Threat = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Error = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileScans", x => x.SongFileId);
                    table.ForeignKey(
                        name: "FK_FileScans_SongFiles_SongFileId",
                        column: x => x.SongFileId,
                        principalTable: "SongFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
            // #17 : les fichiers déjà envoyés sont analysés aussi ; ils restent illisibles jusqu'au verdict
            migrationBuilder.Sql("""
                INSERT INTO FileScans (SongFileId, Status, RequestedAt)
                SELECT Id, N'Pending', SYSDATETIMEOFFSET() FROM SongFiles;

                INSERT INTO BackgroundJobs (Id, Type, Payload, Status, Attempts, CreatedAt, RunAfter)
                SELECT NEWID(), N'file.scan', LOWER(CONVERT(nvarchar(36), Id)), N'Pending', 0,
                       SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET()
                FROM SongFiles;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sans la table, ces tâches n'auraient plus rien à faire
            migrationBuilder.Sql("DELETE FROM BackgroundJobs WHERE Type IN (N'file.scan', N'file.infected-notice') AND Status IN (N'Pending', N'Running');");
            migrationBuilder.DropTable(
                name: "FileScans");
        }
    }
}
