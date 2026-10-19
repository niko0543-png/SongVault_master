using SongVault.Application.Audit;
using SongVault.Application.Common.Exceptions;
using SongVault.Application.Setlists;
using SongVault.Application.Songs.DeleteSong;
using SongVault.Application.Tests.Fakes;
using SongVault.Application.Versions;
using SongVault.Domain.Common;
using SongVault.Domain.Setlists;
using SongVault.Domain.Songs;

namespace SongVault.Application.Tests.Setlists;

public sealed class SetlistHandlersTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid BandId = Guid.CreateVersion7();

    private readonly FakeSongRepository _songs = new();
    private readonly FakeSetlistRepository _setlists;
    private readonly FakeUnitOfWork _uow = new();
    private readonly FixedTimeProvider _clock = new(Now.AddHours(1));

    public SetlistHandlersTests() => _setlists = new FakeSetlistRepository(_songs);

    private (Song Song, SongVersion Version) ExistingSong(string title, int? bpm = null, string? key = null)
    {
        var song = Song.Create(BandId, title, null, null, Now);
        var version = song.AddVersion("v1", SongVersionStatus.Final, null, null, Now, bpm, MusicalKey.ParseOptional(key));
        _songs.Songs.Add(song);
        return (song, version);
    }

    private Setlist ExistingSetlist(string name = "Concert au Ferrailleur")
    {
        var setlist = Setlist.Create(BandId, name, new DateOnly(2026, 11, 28), "Nantes", Now);
        _setlists.Setlists.Add(setlist);
        return setlist;
    }

    private SetSetlistItemsHandler ItemsHandler() => new(_setlists, _songs, _uow, _clock);

    private Task<SetlistDto> SetItems(Setlist setlist, params SetlistItemChoice[] items)
        => ItemsHandler().HandleAsync(new SetSetlistItemsCommand(setlist.Id, items), CancellationToken.None);

    [Fact]
    public async Task SetItems_reordonne_en_deux_passes_dans_une_transaction()
    {
        var setlist = ExistingSetlist();
        var a = ExistingSong("Aube");
        var b = ExistingSong("Brume");
        var c = ExistingSong("Cendres");
        await SetItems(setlist, new(a.Song.Id), new(b.Song.Id), new(c.Song.Id));

        var result = await SetItems(setlist, new(c.Song.Id), new(a.Song.Id), new(b.Song.Id));

        Assert.Equal(["Cendres", "Aube", "Brume"], result.Items.Select(i => i.SongTitle));
        Assert.Equal([1, 2, 3], result.Items.Select(i => i.Position));
        Assert.Equal(2, _uow.TransactionCount);
        Assert.Equal(4, _uow.SaveCount);
    }

    [Fact]
    public async Task Le_total_additionne_exactement_les_durees_saisies_et_audio()
    {
        var setlist = ExistingSetlist();
        var a = ExistingSong("Aube");
        var b = ExistingSong("Brume");
        var c = ExistingSong("Cendres");
        _setlists.AudioDurations[a.Version.Id] = 187_431;                // 3:07,431 lu dans le fichier audio
        _setlists.AudioDurations[b.Version.Id] = 240_000;                // remplacé par la saisie ci-dessous

        var result = await SetItems(setlist, new(a.Song.Id), new(b.Song.Id, DurationMs: 215_000), new(c.Song.Id));

        Assert.Equal([187_431, 215_000, null], result.Items.Select(i => i.DurationMs));
        Assert.Equal(402_431L, result.TotalDurationMs);
        Assert.Equal(1, result.MissingDurationCount);
    }

    [Fact]
    public async Task Tonalite_et_BPM_viennent_de_la_version_jouee()
    {
        var setlist = ExistingSetlist();
        var a = ExistingSong("Aube", 92, "Am");
        var v2 = a.Song.AddVersion("v2 live", SongVersionStatus.Final, null, null, Now, 96, MusicalKey.Parse("Bm"));

        var latest = Assert.Single((await SetItems(setlist, new SetlistItemChoice(a.Song.Id))).Items);
        var chosen = Assert.Single((await SetItems(setlist, new SetlistItemChoice(a.Song.Id, a.Version.Id))).Items);

        Assert.Equal((null, v2.Id, "Bm", 96), (latest.VersionId, latest.PlayedVersionId, latest.Key, latest.Bpm));
        Assert.Equal((a.Version.Id, a.Version.Id, "Am", 92), (chosen.VersionId, chosen.PlayedVersionId, chosen.Key, chosen.Bpm));
        Assert.Equal([1, 2], chosen.Versions.Select(v => v.Number));
    }

    [Fact]
    public async Task SetItems_refuse_la_version_d_un_autre_morceau_sans_enregistrer()
    {
        var setlist = ExistingSetlist();
        var a = ExistingSong("Aube");
        var b = ExistingSong("Brume");

        await Assert.ThrowsAsync<DomainException>(() => SetItems(setlist, new SetlistItemChoice(a.Song.Id, b.Version.Id)));

        Assert.Equal(0, _uow.SaveCount);
        Assert.Empty(setlist.Items);
    }

    [Fact]
    public async Task SetItems_refuse_un_morceau_inconnu_ou_a_la_corbeille()
    {
        var setlist = ExistingSetlist();
        var a = ExistingSong("Aube");
        a.Song.MoveToTrash("ana", Now);

        await Assert.ThrowsAsync<DomainException>(() => SetItems(setlist, new SetlistItemChoice(a.Song.Id)));
        await Assert.ThrowsAsync<DomainException>(() => SetItems(setlist, new SetlistItemChoice(Guid.NewGuid())));
        Assert.Equal(0, _uow.SaveCount);
    }

    [Fact]
    public async Task SetItems_setlist_absente_leve_NotFound()
        => await Assert.ThrowsAsync<NotFoundException>(() => ItemsHandler().HandleAsync(
            new SetSetlistItemsCommand(Guid.NewGuid(), []), CancellationToken.None));

    [Fact]
    public async Task Create_puis_Delete_garde_les_morceaux()
    {
        var a = ExistingSong("Aube");
        var created = await new CreateSetlistHandler(_setlists, _uow, new FakeBandContext(BandId), _clock)
            .HandleAsync(new CreateSetlistCommand("Répétition", null, null), CancellationToken.None);
        await SetItems(_setlists.Setlists.Single(), new SetlistItemChoice(a.Song.Id));

        await new DeleteSetlistHandler(_setlists, _uow).HandleAsync(created.Id, CancellationToken.None);

        Assert.Empty(_setlists.Setlists);
        Assert.Single(_songs.Songs);
        Assert.Equal((0L, 0), (created.TotalDurationMs, created.MissingDurationCount));
    }

    [Fact]
    public async Task Un_morceau_d_une_setlist_ne_va_pas_a_la_corbeille()
    {
        var setlist = ExistingSetlist();
        var a = ExistingSong("Aube");
        await SetItems(setlist, new SetlistItemChoice(a.Song.Id));
        var saves = _uow.SaveCount;

        var error = await Assert.ThrowsAsync<ConflictException>(() => new DeleteSongHandler(
                _songs, new FakeReleaseRepository(), _setlists, _uow, new FakeCurrentUser(), _clock,
                new SongVault.Application.Activity.ActivityLog(new FakeActivityRepository(), new FakeCurrentUser()),
                new AuditLog(new FakeAuditRepository(), new FakeCurrentUser()))
            .HandleAsync(a.Song.Id, CancellationToken.None));

        Assert.Contains("« Concert au Ferrailleur »", error.Message);
        Assert.False(a.Song.IsDeleted);
        Assert.Equal(saves, _uow.SaveCount);
    }

    [Fact]
    public async Task Une_version_choisie_dans_une_setlist_ne_va_pas_a_la_corbeille()
    {
        var setlist = ExistingSetlist();
        var a = ExistingSong("Aube");
        await SetItems(setlist, new SetlistItemChoice(a.Song.Id, a.Version.Id));

        var error = await Assert.ThrowsAsync<ConflictException>(() => new DeleteSongVersionHandler(
                _songs, new FakeReleaseRepository(), _setlists, _uow, new FakeCurrentUser(), _clock,
                new AuditLog(new FakeAuditRepository(), new FakeCurrentUser()))
            .HandleAsync(a.Song.Id, a.Version.Id, CancellationToken.None));

        Assert.Contains("« Concert au Ferrailleur »", error.Message);
        Assert.False(a.Version.IsDeleted);
    }
}