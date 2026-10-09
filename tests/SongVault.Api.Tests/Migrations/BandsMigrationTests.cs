using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using SongVault.Infrastructure.Persistence;

namespace SongVault.Api.Tests.Migrations;

[Collection(ApiCollection.Name)]
public sealed class BandsMigrationTests(SongVaultApiFactory factory)
{
    [Fact]
    public async Task Morceaux_valides_rattaches_au_groupe_de_leur_proprietaire_et_orphelins_supprimes()
    {
        var options = new DbContextOptionsBuilder<SongVaultDbContext>()
            .UseSqlServer(factory.ConnectionStringFor($"MigrationTest_{Guid.NewGuid():N}"))
            .Options;
        await using var db = new SongVaultDbContext(options);

        // 1. Base telle qu'elle était avant les groupes
        await db.GetService<IMigrator>().MigrateAsync("20261007121941_AddSongOwner");

        // 2. Deux comptes ; A1, A2, B1 valides ; « Vide » (OwnerId = '') et « Inconnu » (compte disparu) orphelins
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO AspNetUsers (Id, UserName, NormalizedUserName, Email, NormalizedEmail,
                                     EmailConfirmed, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
            VALUES ('u1', 'alice@test.local', 'ALICE@TEST.LOCAL', 'alice@test.local', 'ALICE@TEST.LOCAL', 0, 0, 0, 1, 0),
                   ('u2', 'bob@test.local',   'BOB@TEST.LOCAL',   'bob@test.local',   'BOB@TEST.LOCAL',   0, 0, 0, 1, 0);

            INSERT INTO Songs (Id, Title, CreatedAt, UpdatedAt, OwnerId, LastVersionNumber) VALUES
                ('10000000-0000-0000-0000-000000000001', 'A1',      SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), 'u1', 1),
                ('10000000-0000-0000-0000-000000000002', 'A2',      SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), 'u1', 0),
                ('10000000-0000-0000-0000-000000000003', 'B1',      SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), 'u2', 0),
                ('10000000-0000-0000-0000-000000000004', 'Vide',    SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), '',   1),
                ('10000000-0000-0000-0000-000000000005', 'Inconnu', SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), 'disparu', 0);

            INSERT INTO SongVersions (Id, SongId, Number, Title, Status, CreatedAt, UpdatedAt) VALUES
                ('20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', 1, 'v1', 'Demo', SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET()),
                ('20000000-0000-0000-0000-000000000004', '10000000-0000-0000-0000-000000000004', 1, 'v1', 'Demo', SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());

            INSERT INTO SongFiles (Id, SongVersionId, OriginalFileName, StorageKey, ContentType, SizeBytes, FileType, UploadedAt) VALUES
                ('30000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001', 'a.mp3', 'a.mp3', 'audio/mpeg', 10, 'Audio', SYSDATETIMEOFFSET()),
                ('30000000-0000-0000-0000-000000000004', '20000000-0000-0000-0000-000000000004', 'o.mp3', 'o.mp3', 'audio/mpeg', 10, 'Audio', SYSDATETIMEOFFSET());
            """);

        // 3. AddBands, BackfillBands, RequireSongBand
        await db.Database.MigrateAsync();

        // 4. Vérifications
        Assert.Equal(2, await CountAsync(db, "SELECT COUNT(*) AS Value FROM Bands WHERE Name IN (N'Groupe de alice', N'Groupe de bob')"));
        Assert.Equal(2, await CountAsync(db, "SELECT COUNT(*) AS Value FROM BandMemberships WHERE Role = 'Owner'"));
        Assert.Equal(3, await CountAsync(db, "SELECT COUNT(*) AS Value FROM Songs"));
        Assert.Equal(3, await CountAsync(db, """
            SELECT COUNT(*) AS Value FROM Songs s JOIN BandMemberships m ON m.BandId = s.BandId
            WHERE (s.Title IN ('A1', 'A2') AND m.UserId = 'u1') OR (s.Title = 'B1' AND m.UserId = 'u2')
            """));
        Assert.Equal(1, await CountAsync(db, "SELECT COUNT(*) AS Value FROM SongVersions"));        // celle de A1
        Assert.Equal(1, await CountAsync(db, "SELECT COUNT(*) AS Value FROM SongFiles WHERE StorageKey = 'a.mp3'"));
        Assert.Equal(1, await CountAsync(db, "SELECT COUNT(*) AS Value FROM SongFiles"));

        await db.Database.EnsureDeletedAsync();
    }

    private static Task<int> CountAsync(SongVaultDbContext db, string sql)
        => db.Database.SqlQueryRaw<int>(sql).SingleAsync();
}