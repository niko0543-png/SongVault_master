using SongVault.Application.Abstractions;

namespace SongVault.Application.Songs.GetSong;

public sealed class GetSongHandler(ISongRepository songs)
{
    public async Task<SongDto?> HandleAsync(Guid id, CancellationToken ct)
    {
        var song = await songs.GetByIdAsync(id, ct);
        return song is null ? null : SongDto.From(song);
    }
}