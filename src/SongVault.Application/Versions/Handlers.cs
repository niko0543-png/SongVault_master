using SongVault.Application.Abstractions;
using SongVault.Application.Activity;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Activity;
using SongVault.Domain.Songs;
using SongVault.Application.Audit;
using SongVault.Application.Trash;
using SongVault.Domain.Audit;
using SongVault.Application.Billing;
using SongVault.Application.Files;
using SongVault.Domain.Common;
using SongVault.Domain.Files;

namespace SongVault.Application.Versions;

public sealed class CreateSongVersionHandler(
    ISongRepository songs, IUnitOfWork unitOfWork, TimeProvider clock, ActivityLog activity,
    IFileStorageService storage, AudioPreviewQueue previews, FileScanQueue scans, PlanLimits limits)
{
    private const int MaxAttempts = 3;

    public async Task<SongVersionDto> HandleAsync(CreateSongVersionCommand command, CancellationToken ct)
    {
        // Validée UNE fois, avant la boucle : une tonalité invalide ne mérite pas de nouvelle tentative
        var key = MusicalKey.ParseOptional(command.Key);

        // ADR 0047 : les copies sont stockées UNE fois, avant la boucle ; la base les rattache ensuite
        var carried = await CopyFilesAsync(command, ct);
        SongVersionDto created;
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                var song = carried is null
                    ? await songs.GetByIdAsync(command.SongId, ct)
                    : await songs.GetWithVersionAsync(command.SongId, carried.SourceVersionId, ct);
                if (song is null) throw new NotFoundException("Song", command.SongId);

                var now = clock.GetUtcNow();
                var version = song.AddVersion(command.Title, command.Status, command.Notes, command.Lyrics,
                    now, command.Bpm, key);
                if (carried is not null)
                {
                    var source = song.FindVersion(carried.SourceVersionId)
                                 ?? throw new NotFoundException("SongVersion", carried.SourceVersionId);
                    foreach (var file in version.CarryOverFiles(source, carried.StorageKeys, now))
                    {
                        previews.RequestFor(file, now);                     // #15, comme un envoi
                        scans.RequestFor(file, now);                        // #17 : la copie est analysée à son tour
                    }
                }
                activity.VersionChanged(ActivityType.VersionAdded, song, version, now);   // oublié aussi par DiscardChanges
                try
                {
                    await unitOfWork.SaveChangesAsync(ct);
                    created = SongVersionDto.From(version);
                    break;
                }
                catch (ConcurrencyConflictException) when (attempt < MaxAttempts)
                {
                    unitOfWork.DiscardChanges();
                    await Task.Delay(Random.Shared.Next(10, 50) * attempt, ct);
                }
            }
        }
        catch when (carried is not null)
        {
            foreach (var storageKey in carried.StorageKeys.Values)
                await storage.DeleteAsync(storageKey, CancellationToken.None);     // compensation
            throw;
        }

        if (carried is not null) await limits.RefreshStorageAsync(carried.BandId, ct);   // jauge (#16)
        return created;
    }

    /// <summary>
    /// Copie dans le stockage les fichiers à reprendre (ADR 0047), après avoir vérifié qu'ils sont en place,
    /// lisibles (#17) et qu'ils tiennent dans le stockage du groupe (#16). null : rien à reprendre.
    /// </summary>
    private async Task<CarriedFiles?> CopyFilesAsync(CreateSongVersionCommand command, CancellationToken ct)
    {
        if (command.CopyFileIds is not { Count: > 0 } ids) return null;
        if (command.CopyFromVersionId is not { } sourceId)
            throw new DomainException("Indiquez la version d'où reprendre les fichiers.");

        var song = await songs.GetWithVersionAsync(command.SongId, sourceId, ct)
                   ?? throw new NotFoundException("Song", command.SongId);
        var source = song.FindVersion(sourceId) is { IsDeleted: false } found
            ? found
            : throw new NotFoundException("SongVersion", sourceId);
        var files = new List<SongFile>();
        foreach (var id in ids.Distinct())
        {
            var file = source.Files.SingleOrDefault(f => f.Id == id && !f.IsDeleted)
                       ?? throw new DomainException("Un fichier repris doit être un fichier en place de la version d'origine.");
            var info = await songs.GetFileAsync(command.SongId, sourceId, id, ct);
            FileScanGate.EnsureReadable(info?.Scan);                       // un fichier bloqué ne se recopie pas
            files.Add(file);
        }
        await limits.EnsureStorageAsync(song.BandId, files.Sum(f => f.SizeBytes), ct);
        unitOfWork.DiscardChanges();                                       // la boucle recharge le morceau

        var storageKeys = new Dictionary<Guid, string>();
        try
        {
            foreach (var file in files)
            {
                var copyKey = $"{Guid.CreateVersion7():N}{Path.GetExtension(file.StorageKey)}";
                await using (var content = await storage.OpenReadAsync(file.StorageKey, ct))
                    await storage.SaveAsync(copyKey, content, ct);
                storageKeys[file.Id] = copyKey;
            }
        }
        catch
        {
            foreach (var copyKey in storageKeys.Values)
                await storage.DeleteAsync(copyKey, CancellationToken.None);
            throw;
        }
        return new CarriedFiles(sourceId, song.BandId, storageKeys);
    }

    private sealed record CarriedFiles(Guid SourceVersionId, Guid BandId, IReadOnlyDictionary<Guid, string> StorageKeys);
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