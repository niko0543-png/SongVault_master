using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;

namespace SongVault.Application.Songs.DeleteSong;

public sealed class DeleteSongHandler(
    ISongRepository songs, IReleaseRepository releases, IUnitOfWork unitOfWork, IFileStorageService storage)
{
    public async Task HandleAsync(Guid id, CancellationToken ct)
    {
        var song = await songs.GetByIdAsync(id, ct) ?? throw new NotFoundException("Song", id);

        // Une sortie publiée ne perd pas une piste en silence (#10) : on retire d'abord le morceau de la sortie
        var onReleases = await releases.ListTitlesWithSongAsync(id, ct);
        if (onReleases.Count > 0)
            throw new ConflictException(
                $"Ce morceau figure sur {string.Join(", ", onReleases.Select(t => $"« {t} »"))}. Retirez-le de ces sorties avant de le supprimer.");

        var keys = await songs.ListStorageKeysAsync(id, ct);    // AVANT la suppression en cascade

        songs.Remove(song);
        await unitOfWork.SaveChangesAsync(ct);                  // la base supprime versions et fichiers en cascade

        foreach (var key in keys)
            await storage.DeleteAsync(key, CancellationToken.None);
    }
}