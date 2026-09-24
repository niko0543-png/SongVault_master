using SongVault.Application._Temporary;

namespace SongVault.Application.Songs.GetSong;

public sealed class GetSongHandler(InMemorySongStore store)
{
    public Task<SongDto?> HandleAsync(Guid id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var song = store.Find(id);
        return Task.FromResult(song is null ? null : SongDto.From(song));
    }
}