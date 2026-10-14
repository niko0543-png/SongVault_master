using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SongVault.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NotifiedAt",
                table: "ActivityEvents",
                type: "datetimeoffset",
                nullable: true);
            // Les événements d'avant #12 ne partent pas par e-mail le jour du déploiement.
            // EXEC : dans un script SQL d'un seul lot, la colonne n'existe pas encore quand le lot est compilé.
            migrationBuilder.Sql("EXEC('UPDATE ActivityEvents SET NotifiedAt = CreatedAt');");

            migrationBuilder.CreateTable(
                name: "NotificationDigestItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    EventId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDigestItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationDigestItems_ActivityEvents_EventId",
                        column: x => x.EventId,
                        principalTable: "ActivityEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotificationDigestItems_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => new { x.UserId, x.Kind });
                    table.ForeignKey(
                        name: "FK_NotificationPreferences_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_NotNotified",
                table: "ActivityEvents",
                column: "Id",
                filter: "[NotifiedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDigestItems_EventId",
                table: "NotificationDigestItems",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDigestItems_UserId_Id",
                table: "NotificationDigestItems",
                columns: new[] { "UserId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationDigestItems");

            migrationBuilder.DropTable(
                name: "NotificationPreferences");

            migrationBuilder.DropIndex(
                name: "IX_ActivityEvents_NotNotified",
                table: "ActivityEvents");

            migrationBuilder.DropColumn(
                name: "NotifiedAt",
                table: "ActivityEvents");
        }
    }
}
