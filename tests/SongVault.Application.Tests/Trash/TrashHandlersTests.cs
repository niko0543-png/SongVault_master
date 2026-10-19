using SongVault.Application.Audit;
using SongVault.Application.Billing;
using SongVault.Application.Common.Exceptions;
using SongVault.Application.Files;
using SongVault.Application.Tests.Fakes;
using SongVault.Application.Trash;
using SongVault.Application.Versions;
using SongVault.Domain.Audit;
using SongVault.Domain.Files;
using SongVault.Domain.Releases;
using SongVault.Domain.Songs;
using SongVault.Domain.Trash;

namespace SongVault.Application.Tests.Trash;

public sealed class TrashHandlersTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid BandId = Guid.CreateVersion7();
    private readonly FakeSongRepository _songs = new();
    private readonly FakeReleaseRepository _releases = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeAuditRepository _audit = new();
    private readonly FakeFileStorageService _storage = new();
    private readonly FakeBillingRepository _billing = new();
    private readonly FakeTrashRepository _trash;

    public TrashHandlersTests() => _trash = new FakeTrashRepository(_songs);

    private AuditLog Audit(string userId = "ana") => new(_audit, new FakeCurrentUser(userId));

    private TrashPurger Purger(DateTimeOffset at) => new(_trash, _storage, _audit, _uow, new PlanLimits(_billing), new FixedTimeProvider(at));

    private (Song Song, SongVersion Version, SongFile File) Existing()
    {
        var song = Song.Create(BandId, "Nocturne", null, null, Now);
        var version = song.AddVersion("Maquette", SongVersionStatus.Demo, null, null, Now);
        var file = version.AddFile("prise.wav", "k.wav", "audio/wav", 10, SongFileType.Audio, Now);
        _songs.Songs.Add(song);
        _storage.Files["k.wav"] = [1];
        return (song, version, file);
    }

    private Task Restore(TrashItemKind kind, Guid id)
        => new RestoreFromTrashHandler(_trash, _uow, Audit(), new FixedTimeProvider(Now.AddDays(1))).HandleAsync(kind, id, CancellationToken.None);

    [Fact]
    public async Task Supprimer_un_fichier_le_met_a_la_corbeille_sans_toucher_au_stockage()
    {
        var (song, version, file) = Existing();

        await new DeleteSongFileHandler(_songs, _uow, new FakeCurrentUser("ana"), new FixedTimeProvider(Now), new SongVault.Application.Activity.ActivityLog(new FakeActivityRepository(), new FakeCurrentUser("ana")), Audit())
            .HandleAsync(song.Id, version.Id, file.Id, CancellationToken.None);

        Assert.Equal((Now, "ana"), (file.DeletedAt, file.DeletedById));
        Assert.Single(_storage.Files);
        Assert.Empty(_billing.Refreshed);
        Assert.Equal(AuditAction.FileDeleted, Assert.Single(_audit.Entries).Action);
    }

    [Fact]
    public async Task Supprimer_une_version_la_met_a_la_corbeille_avec_ses_fichiers()
    {
        var (song, version, file) = Existing();

        await new DeleteSongVersionHandler(_songs, _releases, new FakeSetlistRepository(), _uow, new FakeCurrentUser("ana"), new FixedTimeProvider(Now), Audit())
           .HandleAsync(song.Id, version.Id, CancellationToken.None);

        Assert.True(version.IsDeleted);
        Assert.True(file.IsDeleted);
        Assert.False(song.IsDeleted);
        var entry = Assert.Single(_audit.Entries);
        Assert.Equal((AuditAction.VersionDeleted, "Nocturne — v1"), (entry.Action, entry.Subject));
        Assert.Equal(1, _uow.SaveCount);
    }

    [Fact]
    public async Task Une_version_presente_sur_une_sortie_ne_va_pas_a_la_corbeille()
    {
        var (song, version, _) = Existing();
        var release = Release.Create(BandId, "Nuits blanches", ReleaseType.EP, null, Now);
        release.SetTracks([new(song.Id, version.Id)], Now);
        _releases.Releases.Add(release);

        var error = await Assert.ThrowsAsync<ConflictException>(() => new DeleteSongVersionHandler(
            _songs, _releases, new FakeSetlistRepository(), _uow, new FakeCurrentUser("ana"), new FixedTimeProvider(Now), Audit())
            .HandleAsync(song.Id, version.Id, CancellationToken.None));

        Assert.Contains("« Nuits blanches »", error.Message);
        Assert.False(version.IsDeleted);
        Assert.Equal(0, _uow.SaveCount);
    }

    [Fact]
    public async Task Restaurer_un_morceau_le_remet_en_place_en_entier()
    {
        var (song, version, file) = Existing();
        song.MoveToTrash("ana", Now);

        await Restore(TrashItemKind.Song, song.Id);

        Assert.False(song.IsDeleted);
        Assert.False(version.IsDeleted);
        Assert.False(file.IsDeleted);
        Assert.Equal((AuditAction.SongRestored, "Nocturne"), (_audit.Entries.Single().Action, _audit.Entries.Single().Subject));
        Assert.Equal(1, _uow.SaveCount);
        Assert.Empty(await _trash.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Une_version_d_un_morceau_a_la_corbeille_ne_revient_pas_seule()
    {
        var (song, version, _) = Existing();
        song.TrashVersion(version.Id, "ana", Now);
        song.MoveToTrash("ana", Now.AddHours(1));

        var error = await Assert.ThrowsAsync<ConflictException>(() => Restore(TrashItemKind.Version, version.Id));

        Assert.Contains("« Nocturne » est à la corbeille", error.Message);
        Assert.Equal(0, _uow.SaveCount);
    }

    [Fact]
    public async Task Un_fichier_d_une_version_a_la_corbeille_ne_revient_pas_seul()
    {
        var (song, version, file) = Existing();
        version.TrashFile(file.Id, "ana", Now);
        song.TrashVersion(version.Id, "ana", Now.AddHours(1));

        var error = await Assert.ThrowsAsync<ConflictException>(() => Restore(TrashItemKind.File, file.Id));

        Assert.Contains("La version 1 de « Nocturne » est à la corbeille", error.Message);
    }

    [Fact]
    public async Task Restaurer_un_element_qui_n_est_pas_a_la_corbeille_leve_NotFound()
    {
        var (song, version, file) = Existing();

        await Assert.ThrowsAsync<NotFoundException>(() => Restore(TrashItemKind.Song, song.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => Restore(TrashItemKind.Version, version.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => Restore(TrashItemKind.File, file.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => Restore(TrashItemKind.Song, Guid.NewGuid()));
    }

    [Fact]
    public async Task La_corbeille_montre_une_version_seule_mais_pas_celles_d_un_morceau_supprime()
    {
        var (song, version, _) = Existing();
        var other = Song.Create(BandId, "Aube", null, null, Now);
        var otherVersion = other.AddVersion("Idée", SongVersionStatus.Idea, null, null, Now);
        _songs.Songs.Add(other);
        song.MoveToTrash("ana", Now);
        other.TrashVersion(otherVersion.Id, "leo", Now.AddHours(1));

        var items = await new ListTrashHandler(_trash).HandleAsync(CancellationToken.None);

        Assert.Equal([(TrashItemKind.Version, otherVersion.Id), (TrashItemKind.Song, song.Id)], items.Select(i => (i.Kind, i.Id)));
        Assert.Equal(Now.AddDays(30), items[1].PurgeAt);
        Assert.DoesNotContain(items, i => i.Id == version.Id);
    }

    [Fact]
    public async Task Supprimer_definitivement_efface_le_stockage_puis_les_lignes_puis_libere_la_place()
    {
        var (song, _, _) = Existing();
        song.MoveToTrash("ana", Now);
        var seen = (Storage: -1, Refreshed: -1);
        _trash.OnDelete = () => seen = (_storage.Files.Count, _billing.Refreshed.Count);

        await new DeleteFromTrashHandler(_trash, Purger(Now.AddDays(2)), new FakeBandContext(BandId), new FakeCurrentUser("owner-1"))
            .HandleAsync(TrashItemKind.Song, song.Id, CancellationToken.None);

        Assert.Equal((0, 0), seen);                                      // le stockage d'abord, la jauge après
        Assert.Equal(BandId, Assert.Single(_billing.Refreshed));
        Assert.Equal((TrashItemKind.Song, song.Id), Assert.Single(_trash.Purged));
        var entry = Assert.Single(_audit.Entries);
        Assert.Equal((AuditAction.SongPurged, "owner-1", "Nocturne"), (entry.Action, entry.ActorId, entry.Subject));
    }

    [Fact]
    public async Task Supprimer_definitivement_l_element_d_un_autre_groupe_leve_NotFound()
    {
        var (song, _, _) = Existing();
        song.MoveToTrash("ana", Now);

        await Assert.ThrowsAsync<NotFoundException>(() => new DeleteFromTrashHandler(
                _trash, Purger(Now), new FakeBandContext(Guid.CreateVersion7()), new FakeCurrentUser("owner-1"))
            .HandleAsync(TrashItemKind.Song, song.Id, CancellationToken.None));

        Assert.Single(_storage.Files);
        Assert.Empty(_trash.Purged);
    }

    [Theory]
    [InlineData(29 * 24 + 23, 0)]      // J+29, 23 h : encore restaurable
    [InlineData(30 * 24, 1)]           // J+30 : purgé
    public async Task La_purge_attend_trente_jours(int hours, int expected)
    {
        var (song, _, _) = Existing();
        song.MoveToTrash("ana", Now);
        var at = Now.AddHours(hours);

        var purged = await new PurgeExpiredTrashHandler(_trash, Purger(at), new FixedTimeProvider(at)).HandleAsync(CancellationToken.None);

        Assert.Equal(expected, purged);
        Assert.Equal(1 - expected, _storage.Files.Count);
        Assert.Equal(expected, _billing.Refreshed.Count);
    }

    [Fact]
    public async Task La_purge_efface_un_morceau_une_fois_sans_compter_ses_versions_et_sans_auteur()
    {
        var (song, version, file) = Existing();
        version.TrashFile(file.Id, "ana", Now.AddDays(-40));          // parti avant, puis le morceau entier
        song.MoveToTrash("ana", Now.AddDays(-35));
        var live = Song.Create(BandId, "Aube", null, null, Now);
        _songs.Songs.Add(live);

        var purged = await new PurgeExpiredTrashHandler(_trash, Purger(Now), new FixedTimeProvider(Now)).HandleAsync(CancellationToken.None);

        Assert.Equal(1, purged);                                        // le fichier est parti avec le morceau
        var entry = Assert.Single(_audit.Entries);
        Assert.Equal((AuditAction.SongPurged, (string?)null), (entry.Action, entry.ActorId));
        Assert.Empty(_storage.Files);
        Assert.False(live.IsDeleted);
    }
}