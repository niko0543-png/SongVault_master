using SongVault.Application.Common.Exceptions;
using SongVault.Application.Songs.DeleteSong;
using SongVault.Application.Songs.ListSongs;
using SongVault.Application.Songs.UpdateSong;
using SongVault.Application.Tests.Fakes;
using SongVault.Domain.Common;
using SongVault.Domain.Songs;
using SongVault.Domain.Releases;
using SongVault.Application.Activity;
using SongVault.Application.Billing;

namespace SongVault.Application.Tests.Songs;

public sealed class SongCrudHandlersTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);
    private readonly FakeSongRepository _songs = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FixedTimeProvider _clock = new(Now.AddHours(1));
    private readonly FakeFileStorageService _storage = new();
    private readonly FakeActivityRepository _activity = new();
    private ActivityLog Log() => new(_activity, new FakeCurrentUser());
    private readonly FakeReleaseRepository _releases = new();
    private readonly FakeBillingRepository _billing = new();

    private Song ExistingSong()
    {
        var song = Song.Create(Guid.NewGuid(), "Nocturne", null, null, Now);
        _songs.Songs.Add(song);
        return song;
    }

    [Fact]
    public async Task Update_modifie_le_morceau_et_enregistre()
    {
        var song = ExistingSong();

        await new UpdateSongHandler(_songs, _uow, _clock, Log())
            .HandleAsync(new UpdateSongCommand(song.Id, "Nocturne (v2)", "Band", null), CancellationToken.None);

        Assert.Equal("Nocturne (v2)", song.Title);
        Assert.Equal(Now.AddHours(1), song.UpdatedAt);
        Assert.Equal(1, _uow.SaveCount);
    }

    [Fact]
    public async Task Update_morceau_absent_leve_NotFound_sans_enregistrer()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => new UpdateSongHandler(_songs, _uow, _clock, Log())
            .HandleAsync(new UpdateSongCommand(Guid.NewGuid(), "X", null, null), CancellationToken.None));

        Assert.Equal(0, _uow.SaveCount);
    }

    [Fact]
    public async Task Update_titre_invalide_leve_DomainException_sans_enregistrer()
    {
        var song = ExistingSong();

        await Assert.ThrowsAsync<DomainException>(() => new UpdateSongHandler(_songs, _uow, _clock, Log())
            .HandleAsync(new UpdateSongCommand(song.Id, "  ", null, null), CancellationToken.None));

        Assert.Equal(0, _uow.SaveCount);
    }

    [Fact]
    public async Task Delete_supprime_et_enregistre()
    {
        var song = ExistingSong();

        await new DeleteSongHandler(_songs, _releases, _uow, _storage, _clock, Log(), new PlanLimits(_billing)).HandleAsync(song.Id, CancellationToken.None);

        Assert.Empty(_songs.Songs);
        Assert.Equal(1, _uow.SaveCount);
        Assert.Equal(song.BandId, Assert.Single(_billing.Refreshed));   // la jauge suit la suppression
    }

    [Fact]
    public async Task Delete_refuse_un_morceau_present_sur_une_sortie()
    {
        var song = ExistingSong();
        var version = song.AddVersion("v1", SongVersionStatus.Final, null, null, Now);
        var release = Release.Create(song.BandId, "Nuits blanches", ReleaseType.EP, null, Now);
        release.SetTracks([new(song.Id, version.Id)], Now);
        _releases.Releases.Add(release);

        var error = await Assert.ThrowsAsync<ConflictException>(
            () => new DeleteSongHandler(_songs, _releases, _uow, _storage, _clock, Log(), new PlanLimits(_billing)).HandleAsync(song.Id, CancellationToken.None));

        Assert.Contains("« Nuits blanches »", error.Message);
        Assert.Single(_songs.Songs);
        Assert.Equal(0, _uow.SaveCount);
    }


    [Fact]
    public async Task Delete_morceau_absent_leve_NotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => new DeleteSongHandler(_songs, _releases, _uow, _storage, _clock, Log(), new PlanLimits(_billing)).HandleAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Theory]
    [InlineData(1, 20, 1, 20)]      // valeurs normales inchangées
    [InlineData(0, 20, 1, 20)]      // page < 1 → 1
    [InlineData(1, 0, 1, 1)]        // pageSize < 1 → 1
    [InlineData(3, 1000, 3, 50)]    // pageSize plafonné à 50
    public async Task List_borne_page_et_pageSize(int page, int pageSize, int expectedPage, int expectedSize)
    {
        await new ListSongsHandler(_songs).HandleAsync(new ListSongsQuery(page, pageSize), CancellationToken.None);

        Assert.Equal((expectedPage, expectedSize), _songs.LastListRequest);
    }
}