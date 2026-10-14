using SongVault.Application.Common.Exceptions;
using SongVault.Application.Releases;
using SongVault.Application.Tests.Fakes;
using SongVault.Domain.Common;
using SongVault.Domain.Releases;
using SongVault.Domain.Songs;

namespace SongVault.Application.Tests.Releases;

public sealed class ReleaseHandlersTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid BandId = Guid.CreateVersion7();
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D];

    private readonly FakeSongRepository _songs = new();
    private readonly FakeReleaseRepository _releases;
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeFileStorageService _storage = new();
    private readonly FixedTimeProvider _clock = new(Now.AddHours(1));

    public ReleaseHandlersTests() => _releases = new FakeReleaseRepository(_songs);

    private (Song Song, SongVersion Version) ExistingSong(string title)
    {
        var song = Song.Create(BandId, title, null, null, Now);
        var version = song.AddVersion("v1", SongVersionStatus.Final, null, null, Now);
        _songs.Songs.Add(song);
        return (song, version);
    }

    private Release ExistingRelease(string title = "Nuits blanches", ReleaseType type = ReleaseType.EP)
    {
        var release = Release.Create(BandId, title, type, null, Now);
        _releases.Releases.Add(release);
        return release;
    }

    private SetReleaseTracksHandler TracksHandler() => new(_releases, _songs, _uow, _clock);

    [Fact]
    public async Task SetTracks_enregistre_en_deux_passes_dans_une_transaction()
    {
        var release = ExistingRelease();
        var a = ExistingSong("Aube");
        var b = ExistingSong("Brume");

        var result = await TracksHandler().HandleAsync(new SetReleaseTracksCommand(release.Id,
            [new(b.Song.Id, b.Version.Id), new(a.Song.Id, a.Version.Id)]), CancellationToken.None);

        Assert.Equal(["Brume", "Aube"], result.Tracks.Select(t => t.SongTitle));
        Assert.Equal([1, 2], result.Tracks.Select(t => t.TrackNumber));
        Assert.Equal(1, _uow.TransactionCount);
        Assert.Equal(2, _uow.SaveCount);
    }

    [Fact]
    public async Task SetTracks_garde_la_version_choisie_parmi_celles_du_morceau()
    {
        var release = ExistingRelease();
        var a = ExistingSong("Aube");
        var v2 = a.Song.AddVersion("v2 mix final", SongVersionStatus.Final, null, null, Now);

        var result = await TracksHandler().HandleAsync(
            new SetReleaseTracksCommand(release.Id, [new(a.Song.Id, v2.Id)]), CancellationToken.None);

        var track = Assert.Single(result.Tracks);
        Assert.Equal(v2.Id, track.VersionId);
        Assert.Equal([1, 2], track.Versions.Select(v => v.Number));
    }

    [Fact]
    public async Task SetTracks_refuse_la_version_d_un_autre_morceau_sans_enregistrer()
    {
        var release = ExistingRelease();
        var a = ExistingSong("Aube");
        var b = ExistingSong("Brume");

        await Assert.ThrowsAsync<DomainException>(() => TracksHandler().HandleAsync(
            new SetReleaseTracksCommand(release.Id, [new(a.Song.Id, b.Version.Id)]), CancellationToken.None));

        Assert.Equal(0, _uow.SaveCount);
        Assert.Empty(release.Tracks);
    }

    [Fact]
    public async Task SetTracks_refuse_un_morceau_en_double_sans_enregistrer()
    {
        var release = ExistingRelease();
        var a = ExistingSong("Aube");

        await Assert.ThrowsAsync<DomainException>(() => TracksHandler().HandleAsync(new SetReleaseTracksCommand(release.Id,
            [new(a.Song.Id, a.Version.Id), new(a.Song.Id, a.Version.Id)]), CancellationToken.None));

        Assert.Equal(0, _uow.SaveCount);
    }

    [Fact]
    public async Task Un_morceau_figure_sur_deux_sorties()
    {
        var single = ExistingRelease("Aube", ReleaseType.Single);
        var album = ExistingRelease("Nuits blanches", ReleaseType.Album);
        var a = ExistingSong("Aube");
        var b = ExistingSong("Brume");

        await TracksHandler().HandleAsync(new SetReleaseTracksCommand(single.Id, [new(a.Song.Id, a.Version.Id)]), CancellationToken.None);
        await TracksHandler().HandleAsync(new SetReleaseTracksCommand(album.Id,
            [new(b.Song.Id, b.Version.Id), new(a.Song.Id, a.Version.Id)]), CancellationToken.None);

        Assert.Equal(1, Assert.Single(single.Tracks).TrackNumber);
        Assert.Equal(2, album.Tracks.Single(t => t.SongId == a.Song.Id).TrackNumber);
        Assert.Equal(["Aube", "Nuits blanches"], await _releases.ListTitlesWithSongAsync(a.Song.Id, CancellationToken.None));
    }

    [Fact]
    public async Task SetTracks_sortie_absente_leve_NotFound()
        => await Assert.ThrowsAsync<NotFoundException>(() => TracksHandler().HandleAsync(
            new SetReleaseTracksCommand(Guid.NewGuid(), []), CancellationToken.None));

    [Fact]
    public async Task UploadCover_remplace_l_ancienne_pochette_et_l_efface_du_stockage()
    {
        var release = ExistingRelease();
        var handler = new UploadReleaseCoverHandler(_releases, _uow, _storage, _clock);

        await handler.HandleAsync(new UploadReleaseCoverCommand(release.Id, "recto.PNG", Png.Length, new MemoryStream(Png)), CancellationToken.None);
        var first = release.CoverStorageKey!;
        await handler.HandleAsync(new UploadReleaseCoverCommand(release.Id, "verso.png", Png.Length, new MemoryStream(Png)), CancellationToken.None);

        Assert.EndsWith(".png", release.CoverStorageKey);
        Assert.Equal("image/png", release.CoverContentType);
        Assert.NotEqual(first, release.CoverStorageKey);
        Assert.Equal([release.CoverStorageKey!], _storage.Files.Keys);
    }

    [Fact]
    public async Task UploadCover_refuse_un_contenu_qui_n_est_pas_l_image_annoncee()
    {
        var release = ExistingRelease();
        byte[] mp3 = [.. "ID3"u8, 4, 0];

        await Assert.ThrowsAsync<UnsupportedFileException>(() => new UploadReleaseCoverHandler(_releases, _uow, _storage, _clock)
            .HandleAsync(new UploadReleaseCoverCommand(release.Id, "pochette.jpg", mp3.Length, new MemoryStream(mp3)), CancellationToken.None));

        Assert.Empty(_storage.Files);
        Assert.Null(release.CoverStorageKey);
    }

    [Fact]
    public async Task UploadCover_refuse_une_image_trop_lourde()
    {
        var release = ExistingRelease();

        await Assert.ThrowsAsync<FileTooLargeException>(() => new UploadReleaseCoverHandler(_releases, _uow, _storage, _clock)
            .HandleAsync(new UploadReleaseCoverCommand(release.Id, "pochette.png", CoverImagePolicy.MaxFileSizeBytes + 1,
                new MemoryStream(Png)), CancellationToken.None));
    }

    [Fact]
    public async Task Delete_efface_la_sortie_puis_sa_pochette()
    {
        var release = ExistingRelease();
        await new UploadReleaseCoverHandler(_releases, _uow, _storage, _clock)
            .HandleAsync(new UploadReleaseCoverCommand(release.Id, "recto.png", Png.Length, new MemoryStream(Png)), CancellationToken.None);

        await new DeleteReleaseHandler(_releases, _uow, _storage).HandleAsync(release.Id, CancellationToken.None);

        Assert.Empty(_releases.Releases);
        Assert.Empty(_storage.Files);
    }
}