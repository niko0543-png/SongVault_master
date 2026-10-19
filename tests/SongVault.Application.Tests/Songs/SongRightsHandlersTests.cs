using System.Text;

using SongVault.Application.Audit;
using SongVault.Application.Common.Exceptions;
using SongVault.Application.Files;
using SongVault.Application.Releases;
using SongVault.Application.Songs.Rights;
using SongVault.Application.Tests.Fakes;
using SongVault.Domain.Audit;
using SongVault.Domain.Common;
using SongVault.Domain.Releases;
using SongVault.Domain.Songs;

namespace SongVault.Application.Tests.Songs;

public sealed class SongRightsHandlersTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid BandId = Guid.CreateVersion7();

    private readonly FakeSongRepository _songs = new();
    private readonly FakeReleaseRepository _releases;
    private readonly FakeAuditRepository _audit = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FixedTimeProvider _clock = new(Now.AddHours(1));

    public SongRightsHandlersTests() => _releases = new FakeReleaseRepository(_songs);

    private (Song Song, SongVersion Version) ExistingSong(string title)
    {
        var song = Song.Create(BandId, title, null, null, Now);
        var version = song.AddVersion("Mix final", SongVersionStatus.Final, null, null, Now);
        _songs.Songs.Add(song);
        return (song, version);
    }

    private SetSongRightsHandler SetHandler() => new(_songs, _uow, _clock, new AuditLog(_audit, new FakeCurrentUser("ana")));

    private static CreditChoice C(string name, CreditRole role, decimal share) => new(name, role, share);

    private static string Text(FileDownload file)
    {
        using var reader = new MemoryStream();
        file.Content!.CopyTo(reader);
        return Encoding.UTF8.GetString(reader.ToArray());
    }

    private static byte[] Bytes(FileDownload file)
    {
        using var copy = new MemoryStream();
        file.Content!.CopyTo(copy);
        return copy.ToArray();
    }

    [Fact]
    public async Task SetRights_refuse_un_total_a_99_pourcent_sans_enregistrer_ni_journaliser()
    {
        var (song, _) = ExistingSong("Aube");

        await Assert.ThrowsAsync<DomainException>(() => SetHandler().HandleAsync(new SetSongRightsCommand(song.Id, null,
            [C("Alice", CreditRole.Author, 50m), C("Bruno", CreditRole.Composer, 49m)]), CancellationToken.None));

        Assert.Equal(0, _uow.SaveCount);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task SetRights_enregistre_les_credits_l_iswc_et_une_ligne_d_audit()
    {
        var (song, _) = ExistingSong("Aube");

        var result = await SetHandler().HandleAsync(new SetSongRightsCommand(song.Id, "t0345246801",
            [C("Alice", CreditRole.Author, 50m), C("Bruno", CreditRole.Composer, 50m)]), CancellationToken.None);

        Assert.Equal("T-034.524.680-1", result.Iswc);
        Assert.Equal(100m, result.TotalPercent);
        Assert.Equal(["Alice", "Bruno"], result.Credits.Select(c => c.Name));
        Assert.Equal(1, _uow.SaveCount);
        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditAction.CreditsChanged, entry.Action);
        Assert.Equal("Aube", entry.Subject);
        Assert.Equal("2 crédits, ISWC T-034.524.680-1", entry.Details);
        Assert.Equal("ana", entry.ActorId);
    }

    [Fact]
    public async Task SetRights_refuse_un_iswc_invalide()
    {
        var (song, _) = ExistingSong("Aube");
        await Assert.ThrowsAsync<DomainException>(() => SetHandler().HandleAsync(
            new SetSongRightsCommand(song.Id, "T-034.524.680-2", []), CancellationToken.None));
        Assert.Equal(0, _uow.SaveCount);
    }

    [Fact]
    public async Task SetRights_d_un_morceau_inconnu_ou_a_la_corbeille_renvoie_NotFound()
    {
        var (song, _) = ExistingSong("Aube");
        song.MoveToTrash("ana", Now);

        await Assert.ThrowsAsync<NotFoundException>(() => SetHandler().HandleAsync(
            new SetSongRightsCommand(song.Id, null, []), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => new GetSongRightsHandler(_songs).HandleAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task SetIsrc_pose_le_code_en_forme_lisible_et_le_retire()
    {
        var (song, version) = ExistingSong("Aube");
        var handler = new SetVersionIsrcHandler(_songs, _uow, _clock);

        var set = await handler.HandleAsync(new SetVersionIsrcCommand(song.Id, version.Id, "frz032600001"), CancellationToken.None);
        Assert.Equal("FR-Z03-26-00001", set.Isrc);

        var cleared = await handler.HandleAsync(new SetVersionIsrcCommand(song.Id, version.Id, " "), CancellationToken.None);
        Assert.Null(cleared.Isrc);
        Assert.Equal(2, _uow.SaveCount);
    }

    [Fact]
    public async Task SetIsrc_refuse_un_code_invalide_et_une_version_inconnue()
    {
        var (song, version) = ExistingSong("Aube");
        var handler = new SetVersionIsrcHandler(_songs, _uow, _clock);

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(
            new SetVersionIsrcCommand(song.Id, version.Id, "FR-Z03-26-0001"), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(
            new SetVersionIsrcCommand(song.Id, Guid.NewGuid(), "FR-Z03-26-00001"), CancellationToken.None));
    }

    [Fact]
    public async Task Get_rend_les_versions_en_place_avec_leur_isrc()
    {
        var (song, version) = ExistingSong("Aube");
        var demo = song.AddVersion("Démo", SongVersionStatus.Demo, null, null, Now);
        song.TrashVersion(demo.Id, "ana", Now);
        version.SetIsrc(Isrc.Parse("FR-Z03-26-00001"), Now);

        var rights = await new GetSongRightsHandler(_songs).HandleAsync(song.Id, CancellationToken.None);

        var only = Assert.Single(rights.Versions);
        Assert.Equal("FR-Z03-26-00001", only.Isrc);
        Assert.Empty(rights.Credits);
        Assert.Equal(0m, rights.TotalPercent);
    }

    [Fact]
    public async Task Export_du_morceau_est_lisible_par_Excel_en_francais()
    {
        var (song, _) = ExistingSong("Aube");
        song.SetRights(Iswc.Parse("T-034.524.680-1"),
            [C("Alice", CreditRole.Author, 33.33m), C("Bruno; dit \"Bubu\"", CreditRole.Composer, 33.33m),
             C("Éditions du Nord", CreditRole.Publisher, 33.34m)], Now);

        var file = await new ExportSongCreditsHandler(_songs).HandleAsync(song.Id, CancellationToken.None);
        var bytes = Bytes(file);

        Assert.Equal("text/csv; charset=utf-8", file.ContentType);
        Assert.Equal("credits-Aube.csv", file.FileName);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);   // BOM : Excel lit l'UTF-8 (accents)
        var lines = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n");
        Assert.Equal("Morceau;ISWC;Ayant droit;Rôle;Part (%)", lines[0]);
        Assert.Equal("Aube;T-034.524.680-1;Alice;Auteur;33,33", lines[1]);
        Assert.Equal("Aube;T-034.524.680-1;\"Bruno; dit \"\"Bubu\"\"\";Compositeur;33,33", lines[2]);
        Assert.Equal("Aube;T-034.524.680-1;Éditions du Nord;Éditeur;33,34", lines[3]);
        Assert.Equal("", lines[4]);
    }

    [Fact]
    public async Task Export_d_un_morceau_sans_credit_garde_une_ligne()
    {
        var (song, _) = ExistingSong("Aube");
        var text = Text(await new ExportSongCreditsHandler(_songs).HandleAsync(song.Id, CancellationToken.None));
        Assert.EndsWith("Aube;;;;\r\n", text);
    }

    [Fact]
    public async Task Export_de_la_sortie_donne_une_ligne_par_ayant_droit_de_chaque_piste()
    {
        var a = ExistingSong("Aube");
        var b = ExistingSong("Brume");
        a.Song.SetRights(null, [C("Alice", CreditRole.Author, 60m), C("Bruno", CreditRole.Composer, 40m)], Now);
        a.Version.SetIsrc(Isrc.Parse("FR-Z03-26-00001"), Now);
        var release = Release.Create(BandId, "Nuits blanches", ReleaseType.EP, null, Now);
        release.SetTracks([new ReleaseTrackChoice(a.Song.Id, a.Version.Id), new ReleaseTrackChoice(b.Song.Id, b.Version.Id)], Now);
        _releases.Releases.Add(release);

        var file = await new ExportReleaseCreditsHandler(_releases, _songs).HandleAsync(release.Id, CancellationToken.None);
        var lines = Text(file).TrimStart('\uFEFF').Split("\r\n");

        Assert.Equal("credits-Nuits blanches.csv", file.FileName);
        Assert.Equal("Piste;Morceau;Version;ISRC;ISWC;Ayant droit;Rôle;Part (%)", lines[0]);
        Assert.Equal("1;Aube;v1 Mix final;FR-Z03-26-00001;;Alice;Auteur;60", lines[1]);
        Assert.Equal("1;Aube;v1 Mix final;FR-Z03-26-00001;;Bruno;Compositeur;40", lines[2]);
        Assert.Equal("2;Brume;v1 Mix final;;;;;", lines[3]);
    }

    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("-Alice", "'-Alice")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("Ligne 1\nLigne 2", "\"Ligne 1\nLigne 2\"")]
    [InlineData("Alice", "Alice")]
    public void Une_cellule_qui_ressemble_a_une_formule_est_neutralisee(string value, string expected)
        => Assert.Equal(expected, CreditsCsv.Field(value));

    [Fact]
    public void Le_nom_du_fichier_ne_garde_pas_les_caracteres_interdits()
        => Assert.Equal("credits-AC_DC _ live.csv", CreditsCsv.FileName("credits-AC/DC : live"));
}