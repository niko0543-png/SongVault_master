using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;

namespace SongVault.Application.Songs.DeleteSong;

public sealed class DeleteSongHandler(ISongRepository songs, IUnitOfWork unitOfWork, IFileStorageService storage)
{
    public async Task HandleAsync(Guid id, CancellationToken ct)
    {
        var song = await songs.GetByIdAsync(id, ct) ?? throw new NotFoundException("Song", id);
        var keys = await songs.ListStorageKeysAsync(id, ct);    // AVANT la suppression en cascade

        songs.Remove(song);
        await unitOfWork.SaveChangesAsync(ct);                  // la base supprime versions et fichiers en cascade

        foreach (var key in keys)
            await storage.DeleteAsync(key, CancellationToken.None);
    }
}