using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;

namespace SongVault.Application.Songs.GetSong;

public sealed class GetSongHandler(ISongRepository songs)
{
    public async Task<SongDto> HandleAsync(Guid id, CancellationToken ct)
    {
        var song = await songs.GetByIdAsync(id, ct) ?? throw new NotFoundException("Song", id);
        return SongDto.From(song);
    }
}