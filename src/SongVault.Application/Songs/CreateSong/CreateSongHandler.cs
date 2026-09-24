using SongVault.Application._Temporary;
using SongVault.Domain.Songs;

namespace SongVault.Application.Songs.CreateSong;

public sealed class CreateSongHandler(InMemorySongStore store, TimeProvider clock)
{
    public Task<SongDto> HandleAsync(CreateSongCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var song = Song.Create(command.Title, command.Artist, command.Description, clock.GetUtcNow());
        store.Add(song);
        return Task.FromResult(SongDto.From(song));
    }
}