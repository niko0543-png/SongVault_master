using SongVault.Application.Abstractions;
using SongVault.Application.Activity;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Activity;
using SongVault.Application.Audit;
using SongVault.Domain.Audit;

namespace SongVault.Application.Songs.DeleteSong;

/// <summary>
/// Met le morceau à la corbeille (#20, ADR 0027), avec ses versions et leurs fichiers : rien ne part du stockage,
/// et la place reste comptée jusqu'à la purge. Restaurable 30 jours.
/// </summary>
public sealed class DeleteSongHandler(
    ISongRepository songs, IReleaseRepository releases, ISetlistRepository setlists, IUnitOfWork unitOfWork,
    ICurrentUser user, TimeProvider clock, ActivityLog activity, AuditLog audit)
{
    public async Task HandleAsync(Guid id, CancellationToken ct)
    {
        var song = await songs.GetWithAllVersionsAsync(id, ct) ?? throw new NotFoundException("Song", id);

        // Une sortie publiée ne perd pas une piste en silence (#10) : on retire d'abord le morceau de la sortie
        var onReleases = await releases.ListTitlesWithSongAsync(id, ct);
        if (onReleases.Count > 0)
            throw new ConflictException(
                $"Ce morceau figure sur {string.Join(", ", onReleases.Select(t => $"« {t} »"))}. Retirez-le de ces sorties avant de le supprimer.");
        // Ni d'une setlist (#21) : la feuille d'un concert ne change pas sans qu'on le voie
        var onSetlists = await setlists.ListNamesWithSongAsync(id, ct);
        if (onSetlists.Count > 0)
            throw new ConflictException(
                $"Ce morceau figure dans les setlists {string.Join(", ", onSetlists.Select(n => $"« {n} »"))}. Retirez-le de ces setlists avant de le supprimer.");

        var now = clock.GetUtcNow();
        song.MoveToTrash(user.UserId, now);
        activity.SongChanged(ActivityType.SongDeleted, song, now);    // le titre reste dans le fil
        audit.Record(song.BandId, AuditAction.SongDeleted, song.Title, now);   // #19 : et au journal d'audit
        await unitOfWork.SaveChangesAsync(ct);
    }
}