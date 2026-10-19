using SongVault.Application.Abstractions;
using SongVault.Application.Activity;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Activity;
using SongVault.Domain.Songs;
using SongVault.Application.Audit;
using SongVault.Application.Trash;
using SongVault.Domain.Audit;

namespace SongVault.Application.Versions;

public sealed class CreateSongVersionHandler(
    ISongRepository songs, IUnitOfWork unitOfWork, TimeProvider clock, ActivityLog activity)
{
    private const int MaxAttempts = 3;

    public async Task<SongVersionDto> HandleAsync(CreateSongVersionCommand command, CancellationToken ct)
    {
        // Validée UNE fois, avant la boucle : une tonalité invalide ne mérite pas de nouvelle tentative
        var key = MusicalKey.ParseOptional(command.Key);

        for (var attempt = 1; ; attempt++)
        {
            var song = await songs.GetByIdAsync(command.SongId, ct)
                       ?? throw new NotFoundException("Song", command.SongId);

            var now = clock.GetUtcNow();
            var version = song.AddVersion(command.Title, command.Status, command.Notes, command.Lyrics,
                now, command.Bpm, key);
            activity.VersionChanged(ActivityType.VersionAdded, song, version, now);   // oublié aussi par DiscardChanges
            try
            {
                await unitOfWork.SaveChangesAsync(ct);
                return SongVersionDto.From(version);
            }
            catch (ConcurrencyConflictException) when (attempt < MaxAttempts)
            {
                unitOfWork.DiscardChanges();
                await Task.Delay(Random.Shared.Next(10, 50) * attempt, ct);
            }
        }
    }
}

public sealed class ListSongVersionsHandler(ISongRepository songs)
{
    public async Task<IReadOnlyList<SongVersionSummaryDto>> HandleAsync(Guid songId, CancellationToken ct)
    {
        if (!await songs.ExistsAsync(songId, ct)) throw new NotFoundException("Song", songId);
        return await songs.ListVersionsAsync(songId, ct);
    }
}

public sealed class GetSongVersionHandler(ISongRepository songs)
{
    public async Task<SongVersionDto> HandleAsync(Guid songId, Guid versionId, CancellationToken ct)
        => await songs.GetVersionAsync(songId, versionId, ct)
           ?? throw new NotFoundException("SongVersion", versionId);
}

public sealed class UpdateSongVersionHandler(
    ISongRepository songs, IUnitOfWork unitOfWork, TimeProvider clock, ActivityLog activity)
{
    public async Task HandleAsync(UpdateSongVersionCommand command, CancellationToken ct)
    {
        var key = MusicalKey.ParseOptional(command.Key);

        var song = await songs.GetWithVersionAsync(command.SongId, command.VersionId, ct)
                   ?? throw new NotFoundException("Song", command.SongId);
        var version = song.FindVersion(command.VersionId)
                      ?? throw new NotFoundException("SongVersion", command.VersionId);

        var now = clock.GetUtcNow();
        version.Update(command.Title, command.Status, command.Notes, command.Lyrics, now, command.Bpm, key);
        activity.VersionChanged(ActivityType.VersionUpdated, song, version, now);
        await unitOfWork.SaveChangesAsync(ct);
    }
}

/// <summary>Met une version et ses fichiers à la corbeille (#20) ; refusé si une sortie ou une setlist (#21) l'utilise.</summary>
public sealed class DeleteSongVersionHandler(
    ISongRepository songs, IReleaseRepository releases, ISetlistRepository setlists, IUnitOfWork unitOfWork,
    ICurrentUser user, TimeProvider clock, AuditLog audit)
{
    public async Task HandleAsync(Guid songId, Guid versionId, CancellationToken ct)
    {
        var song = await songs.GetWithVersionAsync(songId, versionId, ct) ?? throw new NotFoundException("Song", songId);
        if (song.FindVersion(versionId) is null) throw new NotFoundException("SongVersion", versionId);

        var onReleases = await releases.ListTitlesWithVersionAsync(versionId, ct);
        if (onReleases.Count > 0)
            throw new ConflictException(
                $"Cette version figure sur {string.Join(", ", onReleases.Select(t => $"« {t} »"))}. Choisissez-en une autre pour ces sorties avant de la supprimer.");
        var onSetlists = await setlists.ListNamesWithVersionAsync(versionId, ct);
        if (onSetlists.Count > 0)
            throw new ConflictException(
                $"Cette version est choisie dans les setlists {string.Join(", ", onSetlists.Select(n => $"« {n} »"))}. Choisissez-en une autre (ou « dernière version ») avant de la supprimer.");
        var now = clock.GetUtcNow();
        var version = song.TrashVersion(versionId, user.UserId, now) ?? throw new NotFoundException("SongVersion", versionId);
        audit.Record(song.BandId, AuditAction.VersionDeleted, TrashPurger.VersionSubject(song.Title, version.Number), now);
        await unitOfWork.SaveChangesAsync(ct);
    }
}