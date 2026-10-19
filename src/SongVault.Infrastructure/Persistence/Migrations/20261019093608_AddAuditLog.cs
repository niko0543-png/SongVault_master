using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SongVault.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Network = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Device = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountEvents_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TargetUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditEntries_Bands_BandId",
                        column: x => x.BandId,
                        principalTable: "Bands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountEvents_OccurredAt",
                table: "AccountEvents",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_AccountEvents_UserId_OccurredAt",
                table: "AccountEvents",
                columns: new[] { "UserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_BandId_OccurredAt_Id",
                table: "AuditEntries",
                columns: new[] { "BandId", "OccurredAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_OccurredAt",
                table: "AuditEntries",
                column: "OccurredAt");

            // #19 : le journal ne se modifie pas, ni depuis l'application ni par une requête SQL de l'API (ADR 0026).
            // Une suppression passe seulement si le groupe n'existe plus (cascade de la purge d'un compte)
            // ou si la ligne a plus de 364 jours (rétention d'un an, RetentionWorker ; un jour de marge d'horloge).
            migrationBuilder.Sql("""
                CREATE TRIGGER TR_AuditEntries_ReadOnly ON AuditEntries
                AFTER UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM inserted)
                        THROW 51000, N'Journal d''audit : une ligne ne se modifie pas.', 1;
                    IF EXISTS (SELECT 1 FROM deleted d
                               JOIN Bands b ON b.Id = d.BandId
                               WHERE d.OccurredAt > DATEADD(day, -364, SYSDATETIMEOFFSET()))
                        THROW 51000, N'Journal d''audit : une ligne ne se supprime pas.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountEvents");

            migrationBuilder.DropTable(
                name: "AuditEntries");
        }
    }
}
