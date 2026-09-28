using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;

namespace SongVault.Application.Songs.DeleteSong;

public sealed class DeleteSongHandler(ISongRepository songs, IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(Guid id, CancellationToken ct)
    {
        var song = await songs.GetByIdAsync(id, ct) ?? throw new NotFoundException("Song", id);
        songs.Remove(song);
        await unitOfWork.SaveChangesAsync(ct);
    }
}