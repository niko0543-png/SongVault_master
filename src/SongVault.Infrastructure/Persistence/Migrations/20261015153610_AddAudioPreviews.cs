using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SongVault.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioPreviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AudioPreviews",
                columns: table => new
                {
                    SongFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PreviewStorageKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Peaks = table.Column<byte[]>(type: "varbinary(1000)", maxLength: 1000, nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioPreviews", x => x.SongFileId);
                    table.ForeignKey(
                        name: "FK_AudioPreviews_SongFiles_SongFileId",
                        column: x => x.SongFileId,
                        principalTable: "SongFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
            // #15 : les fichiers audio déjà envoyés ont aussi leur aperçu, préparé par la tâche de fond
            migrationBuilder.Sql("""
                INSERT INTO AudioPreviews (SongFileId, Status, RequestedAt)
                SELECT Id, N'Pending', SYSDATETIMEOFFSET() FROM SongFiles WHERE FileType = N'Audio';

                INSERT INTO BackgroundJobs (Id, Type, Payload, Status, Attempts, CreatedAt, RunAfter)
                SELECT NEWID(), N'audio.preview', LOWER(CONVERT(nvarchar(36), Id)), N'Pending', 0,
                       SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET()
                FROM SongFiles WHERE FileType = N'Audio';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sans la table, ces tâches n'auraient plus rien à faire
            migrationBuilder.Sql("DELETE FROM BackgroundJobs WHERE Type = N'audio.preview' AND Status IN (N'Pending', N'Running');");

            migrationBuilder.DropTable(
                name: "AudioPreviews");
        }
    }
}
