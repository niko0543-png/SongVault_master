using SongVault.Application.Common.Exceptions;
using SongVault.Application.Tests.Fakes;
using SongVault.Application.Versions;
using SongVault.Domain.Songs;

namespace SongVault.Application.Tests.Versions;

public sealed class CreateSongVersionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 9, 0, 0, TimeSpan.Zero);
    private readonly FakeSongRepository _songs = new();
    private readonly FakeUnitOfWork _uow = new();

    private CreateSongVersionHandler Sut() => new(_songs, _uow, new FixedTimeProvider(Now));

    private CreateSongVersionCommand CommandForNewSong()
    {
        var song = Song.Create("owner-1", "Nocturne", null, null, Now);
        _songs.Songs.Add(song);
        return new CreateSongVersionCommand(song.Id, "v1", SongVersionStatus.Demo, null, null);
    }

    [Fact]
    public async Task Morceau_absent_leve_NotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Sut().HandleAsync(
            new CreateSongVersionCommand(Guid.NewGuid(), "v1", SongVersionStatus.Demo, null, null), CancellationToken.None));
    }

    [Fact]
    public async Task Un_conflit_puis_succes_retente_une_fois()
    {
        var command = CommandForNewSong();
        _uow.FailuresToSimulate = 1;

        await Sut().HandleAsync(command, CancellationToken.None);

        Assert.Equal(2, _uow.SaveCount);
        Assert.Equal(1, _uow.DiscardCount);
    }

    [Fact]
    public async Task Trois_conflits_propagent_l_exception()
    {
        var command = CommandForNewSong();
        _uow.FailuresToSimulate = 3;

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => Sut().HandleAsync(command, CancellationToken.None));

        Assert.Equal(3, _uow.SaveCount);
        Assert.Equal(2, _uow.DiscardCount);
    }
}