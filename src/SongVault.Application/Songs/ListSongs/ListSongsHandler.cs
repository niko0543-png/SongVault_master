using SongVault.Application.Abstractions;

namespace SongVault.Application.Songs.ListSongs;

public sealed class ListSongsHandler(ISongRepository songs)
{
    public Task<IReadOnlyList<SongDto>> HandleAsync(CancellationToken ct) => songs.ListAsync(ct);
}