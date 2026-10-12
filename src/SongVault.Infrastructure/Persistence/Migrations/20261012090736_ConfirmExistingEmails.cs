using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SongVault.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfirmExistingEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // #4 (ADR 0010) : la connexion exige désormais une adresse confirmée.
            // Les comptes créés avant n'ont jamais reçu d'e-mail : ils sont marqués confirmés pour ne bloquer personne.
            migrationBuilder.Sql("UPDATE AspNetUsers SET EmailConfirmed = 1 WHERE EmailConfirmed = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rien à défaire : on ne sait plus quels comptes étaient confirmés avant
        }
    }
}
