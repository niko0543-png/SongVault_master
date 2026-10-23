using SongVault.Application.Audit;
using SongVault.Application.Billing;
using SongVault.Application.Common.Exceptions;
using SongVault.Application.Releases;
using SongVault.Application.Tests.Fakes;
using SongVault.Application.Trash;
using SongVault.Domain.Audit;
using SongVault.Domain.Bands;
using SongVault.Domain.Common;
using SongVault.Domain.Files;
using SongVault.Domain.Releases;
using SongVault.Domain.Setlists;
using SongVault.Domain.Songs;

namespace SongVault.Application.Tests.Releases;

/// <summary>#38 : les infos de sortie de la dernière version envoient le morceau dans l'onglet Sorties.</summary>
public sealed class SongReleaseHandlersTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid BandId = Guid.CreateVersion7();

    private readonly FakeSongRepository _songs = new();
    private readonly FakeReleaseRepository _releases;
    private readonly FakeSetlistRepository _setlists = new();
    private readonly FakeTrashRepository _trash;
    private readonly FakeAuditRepository _audit = new();
    private readonly FakeFileStorageService _storage = new();
    private readonly FakeBillingRepository _billing = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FixedTimeProvider _clock = new(Now.AddHours(1));

    public SongReleaseHandlersTests()
    {
        _releases = new FakeReleaseRepository(_songs);
        _trash = new FakeTrashRepository(_songs);
    }

    private ReleaseSongHandler Handler(BandRole role = BandRole.Owner)
    {
        var user = new FakeCurrentUser("ana");
        return new ReleaseSongHandler(_songs, _releases, _setlists, _trash,
            new TrashPurger(_trash, _storage, _audit, _uow, new PlanLimits(_billing), _clock),
            _uow, new FakeBandContext(BandId, role), user, new AuditLog(_audit, user), _clock);
    }

    /// <summary>Nocturne : v1 Démo, v2 Studio et v3 Final, chacune avec un fichier dans le stockage.</summary>
    private (Song Song, SongVersion V1, SongVersion V2, SongVersion V3) Nocturne()
    {
        var song = Song.Create(BandId, "Nocturne", null, null, Now);
        var versions = new[] { SongVersionStatus.Demo, SongVersionStatus.Studio, SongVersionStatus.Final }
            .Select((status, i) =>
            {
                var v = song.AddVersion($"v{i + 1}", status, null, null, Now);
                v.AddFile($"prise{i + 1}.wav", $"k{i + 1}.wav", "audio/wav", 100, SongFileType.Audio, Now);
                _storage.Files[$"k{i + 1}.wav"] = [1];
                return v;
            }).ToArray();
        _songs.Songs.Add(song);
        return (song, versions[0], versions[1], versions[2]);
    }

    private static ReleaseSongCommand Command(Song song, SongVersion version, KeepVersions keep = KeepVersions.All,
        bool purgeNow = false, Guid? releaseId = null, IReadOnlyList<string>? links = null)
        => new(song.Id, version.Id, releaseId, new DateOnly(2026, 11, 20), "Nuit Records", "036000291452",
            links ?? ["https://open.spotify.com/track/1"], keep, purgeNow);

    [Fact]
    public async Task Sans_sortie_choisie_un_single_au_titre_du_morceau_est_cree()
    {
        var (song, v1, v2, v3) = Nocturne();

        var result = await Handler().HandleAsync(Command(song, v3), CancellationToken.None);

        var release = Assert.Single(_releases.Releases);
        Assert.Equal(release.Id, result.ReleaseId);
        Assert.Equal(("Nocturne", ReleaseType.Single, new DateOnly(2026, 11, 20)), (release.Title, release.Type, release.ReleaseDate));
        Assert.Equal(("Nuit Records", "036000291452"), (release.Label, release.Upc!.Value));
        Assert.Equal(StreamingPlatform.Spotify, Assert.Single(release.StreamingLinks).Platform);
        Assert.Equal((song.Id, v3.Id), (Assert.Single(release.Tracks).SongId, release.Tracks[0].VersionId));
        Assert.Equal(release.Id, song.ReleaseId);
        Assert.False(v1.IsDeleted || v2.IsDeleted);
        Assert.Empty(result.RemovedVersions);
        Assert.Equal(AuditAction.SongReleased, Assert.Single(_audit.Entries).Action);
        Assert.Equal(1, _uow.TransactionCount);
    }

    [Fact]
    public async Task Le_morceau_peut_ouvrir_un_EP_a_un_autre_titre()
    {
        var (song, _, _, v3) = Nocturne();

        await Handler().HandleAsync(Command(song, v3) with { NewReleaseType = ReleaseType.EP, NewReleaseTitle = "Nuits blanches" },
            CancellationToken.None);

        var release = Assert.Single(_releases.Releases);
        Assert.Equal(("Nuits blanches", ReleaseType.EP), (release.Title, release.Type));
    }

    [Fact]
    public async Task Le_morceau_peut_rejoindre_une_sortie_existante_et_y_prend_la_version_qui_sort()
    {
        var (song, v1, _, v3) = Nocturne();
        var ep = Release.Create(BandId, "Nuits blanches", ReleaseType.EP, null, Now);
        var other = Guid.CreateVersion7();
        ep.SetTracks([new(other, Guid.CreateVersion7()), new(song.Id, v1.Id)], Now);
        _releases.Releases.Add(ep);

        await Handler().HandleAsync(Command(song, v3, releaseId: ep.Id), CancellationToken.None);

        Assert.Single(_releases.Releases);
        Assert.Equal(("Nuits blanches", ReleaseType.EP), (ep.Title, ep.Type));
        Assert.Equal(new DateOnly(2026, 11, 20), ep.ReleaseDate);
        Assert.Equal([(1, other), (2, song.Id)], ep.Tracks.Select(t => (t.TrackNumber, t.SongId)));
        Assert.Equal(v3.Id, ep.Tracks[1].VersionId);
        Assert.Equal(ep.Id, song.ReleaseId);
    }

    [Fact]
    public async Task Seulement_la_derniere_met_les_autres_versions_a_la_corbeille()
    {
        var (song, v1, v2, v3) = Nocturne();

        var result = await Handler().HandleAsync(Command(song, v3, KeepVersions.LatestOnly), CancellationToken.None);

        Assert.True(v1.IsDeleted && v2.IsDeleted);
        Assert.False(v3.IsDeleted);
        Assert.Equal([1, 2], result.RemovedVersions);
        Assert.False(result.Purged);
        Assert.Equal(3, _storage.Files.Count);                         // restaurables 30 jours
        Assert.Equal([AuditAction.VersionDeleted, AuditAction.VersionDeleted, AuditAction.SongReleased],
            _audit.Entries.Select(e => e.Action));
        Assert.Contains("2 versions à la corbeille", _audit.Entries[^1].Details);
    }

    [Fact]
    public async Task Effacer_tout_de_suite_libere_le_stockage_et_rafraichit_la_jauge()
    {
        var (song, _, _, v3) = Nocturne();

        var result = await Handler().HandleAsync(Command(song, v3, KeepVersions.LatestOnly, purgeNow: true), CancellationToken.None);

        Assert.True(result.Purged);
        Assert.Equal(["k3.wav"], _storage.Files.Keys);
        Assert.Equal(2, _trash.Purged.Count);
        Assert.Contains(BandId, _billing.Refreshed);
        Assert.Contains(_audit.Entries, e => e.Action == AuditAction.VersionPurged);
    }

    [Fact]
    public async Task Une_version_utilisee_ailleurs_reste_et_la_reponse_dit_ou()
    {
        var (song, v1, v2, v3) = Nocturne();
        var live = Release.Create(BandId, "Live 2025", ReleaseType.Album, null, Now);
        live.SetTracks([new(song.Id, v1.Id)], Now);
        _releases.Releases.Add(live);
        var setlist = Setlist.Create(BandId, "Concert du 12", null, null, Now);
        setlist.SetItems([new SetlistItemChoice(song.Id, v2.Id)], Now);
        _setlists.Setlists.Add(setlist);

        var result = await Handler().HandleAsync(Command(song, v3, KeepVersions.LatestOnly), CancellationToken.None);

        Assert.False(v1.IsDeleted || v2.IsDeleted);
        Assert.Empty(result.RemovedVersions);
        Assert.Equal([1, 2], result.KeptVersions.Select(k => k.Number));
        Assert.Equal(["Live 2025"], result.KeptVersions[0].Releases);
        Assert.Equal(["Concert du 12"], result.KeptVersions[1].Setlists);
    }

    [Fact]
    public async Task Effacer_tout_de_suite_est_reserve_aux_proprietaires()
    {
        var (song, v1, _, v3) = Nocturne();

        await Assert.ThrowsAsync<ForbiddenException>(() => Handler(BandRole.Member)
            .HandleAsync(Command(song, v3, KeepVersions.LatestOnly, purgeNow: true), CancellationToken.None));
        Assert.False(song.IsReleased || v1.IsDeleted);
        Assert.Empty(_releases.Releases);
    }

    [Fact]
    public async Task Un_membre_peut_garder_seulement_la_derniere_sans_effacer()
    {
        var (song, v1, _, v3) = Nocturne();

        await Handler(BandRole.Member).HandleAsync(Command(song, v3, KeepVersions.LatestOnly), CancellationToken.None);

        Assert.True(v1.IsDeleted);
    }

    [Fact]
    public async Task Une_version_qui_n_est_pas_la_derniere_ne_sort_pas_et_rien_n_est_enregistre()
    {
        var (song, _, v2, _) = Nocturne();

        await Assert.ThrowsAsync<DomainException>(() => Handler().HandleAsync(Command(song, v2), CancellationToken.None));
        Assert.Empty(_releases.Releases);
        Assert.False(song.IsReleased);
        Assert.Equal(0, _uow.SaveCount);
    }

    [Fact]
    public async Task Un_lien_invalide_bloque_tout_avant_la_moindre_ecriture()
    {
        var (song, _, _, v3) = Nocturne();

        await Assert.ThrowsAsync<DomainException>(() => Handler().HandleAsync(
            Command(song, v3, links: ["spotify"]), CancellationToken.None));
        Assert.Equal(0, _uow.SaveCount);
        Assert.False(song.IsReleased);
    }

    [Fact]
    public async Task Un_morceau_deja_sorti_est_refuse()
    {
        var (song, _, _, v3) = Nocturne();
        await Handler().HandleAsync(Command(song, v3), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => Handler().HandleAsync(Command(song, v3), CancellationToken.None));
    }

    [Fact]
    public async Task Remettre_en_travail_garde_la_sortie()
    {
        var (song, _, _, v3) = Nocturne();
        await Handler().HandleAsync(Command(song, v3), CancellationToken.None);

        await new UnreleaseSongHandler(_songs, _uow, _clock).HandleAsync(song.Id, CancellationToken.None);

        Assert.False(song.IsReleased);
        Assert.Single(Assert.Single(_releases.Releases).Tracks);
    }

    [Fact]
    public async Task Retirer_le_morceau_de_sa_sortie_le_remet_en_travail()
    {
        var (song, _, _, v3) = Nocturne();
        var result = await Handler().HandleAsync(Command(song, v3), CancellationToken.None);

        await new SetReleaseTracksHandler(_releases, _songs, _uow, _clock)
            .HandleAsync(new SetReleaseTracksCommand(result.ReleaseId, []), CancellationToken.None);

        Assert.False(song.IsReleased);
    }

    [Fact]
    public async Task SetDistribution_remplace_les_infos_et_renvoie_la_sortie()
    {
        var release = Release.Create(BandId, "Nuits blanches", ReleaseType.EP, null, Now);
        _releases.Releases.Add(release);

        var dto = await new SetReleaseDistributionHandler(_releases, _uow, _clock).HandleAsync(
            new SetReleaseDistributionCommand(release.Id, "Nuit Records", "4006381333931", ["https://www.deezer.com/album/1"]),
            CancellationToken.None);

        Assert.Equal(("Nuit Records", "4006381333931"), (dto.Label, dto.Upc));
        Assert.Equal(StreamingPlatform.Deezer, Assert.Single(dto.StreamingLinks!).Platform);
        Assert.Equal(1, _uow.SaveCount);
    }
}
