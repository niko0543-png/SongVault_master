using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SongVault.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillBands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
    -- 1. Morceaux sans propriétaire valide (OwnerId = '' par défaut d'AddSongOwner, ou compte disparu) :
    --    décision du 9 octobre 2026, ils sont supprimés. Versions et fichiers suivent par ON DELETE CASCADE.
    DELETE FROM Songs
    WHERE OwnerId NOT IN (SELECT Id FROM AspNetUsers);

    -- 2. Un groupe par utilisateur existant, nommé d'après la partie locale de l'e-mail
    CREATE TABLE #Map (UserId nvarchar(450) PRIMARY KEY, BandId uniqueidentifier NOT NULL);
    INSERT INTO #Map (UserId, BandId) SELECT Id, NEWID() FROM AspNetUsers;

    INSERT INTO Bands (Id, Name, CreatedAt)
    SELECT m.BandId,
           LEFT(CONCAT(N'Groupe de ', LEFT(u.UserName, CHARINDEX('@', u.UserName + '@') - 1)), 100),
           SYSDATETIMEOFFSET()
    FROM #Map m JOIN AspNetUsers u ON u.Id = m.UserId;

    INSERT INTO BandMemberships (BandId, UserId, Role, JoinedAt)
    SELECT BandId, UserId, 'Owner', SYSDATETIMEOFFSET() FROM #Map;

    -- 3. Rattachement des morceaux restants
    UPDATE s SET s.BandId = m.BandId
    FROM Songs s JOIN #Map m ON m.UserId = s.OwnerId;

    IF EXISTS (SELECT 1 FROM Songs WHERE BandId IS NULL)
        THROW 50001, 'Morceaux sans groupe apres migration', 1;
    """);

        // Les morceaux supprimés ne reviennent pas avec Down : seule la sauvegarde les restaure.
        protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
    UPDATE Songs SET BandId = NULL;
    DELETE FROM BandMemberships;
    DELETE FROM Bands;
    """);
    }
}
