using SongVault.Application.Abstractions;
using SongVault.Application.Activity;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Activity;
using SongVault.Application.Billing;

namespace SongVault.Application.Songs.DeleteSong;

public sealed class DeleteSongHandler(
    ISongRepository songs, IReleaseRepository releases, IUnitOfWork unitOfWork, IFileStorageService storage,
    TimeProvider clock, ActivityLog activity, PlanLimits limits)
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
        activity.SongChanged(ActivityType.SongDeleted, song, clock.GetUtcNow());    // le titre reste dans le fil
        await unitOfWork.SaveChangesAsync(ct);                  // la base supprime versions et fichiers en cascade
        await limits.RefreshStorageAsync(song.BandId, ct);      // #16 : la place libérée se voit tout de suite

        foreach (var key in keys)
            await storage.DeleteAsync(key, CancellationToken.None);
    }
}