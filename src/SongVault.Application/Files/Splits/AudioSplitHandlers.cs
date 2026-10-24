using SongVault.Application.Abstractions;
using SongVault.Application.Billing;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Activity;
using SongVault.Domain.Common;
using SongVault.Domain.Files;
using SongVault.Domain.Songs;

namespace SongVault.Application.Files.Splits;

/// <summary>
/// Demande le découpage d'un fichier audio en passages (ADR 0050). Tout ce qui peut se vérifier tout de suite l'est :
/// fichier lisible et aperçu prêt (sa durée borne les passages), morceaux choisis en place, place dans le stockage.
/// </summary>
public sealed class RequestAudioSplitHandler(
    IAudioSplitRepository splits, IBandContext band, ICurrentUser user, IBackgroundJobs jobs, IUnitOfWork unitOfWork,
    PlanLimits limits, TimeProvider clock)
{
    public async Task<AudioSplitDto> HandleAsync(RequestAudioSplitCommand command, CancellationToken ct)
    {
        var bandId = band.RequiredBandId;
        var source = await splits.GetSourceAsync(bandId, command.SongId, command.VersionId, command.FileId, ct)
                     ?? throw new NotFoundException("SongFile", command.FileId);
        if (source.FileType != SongFileType.Audio) throw new DomainException("Seul un fichier audio se découpe.");
        FileScanGate.EnsureReadable(source.Scan);                            // #17
        if (source is not { Preview: AudioPreviewStatus.Ready, DurationMs: { } durationMs })
            throw new ConflictException("L'aperçu de ce fichier n'est pas prêt : sa durée n'est pas encore connue.");
        if (await splits.GetLatestAsync(bandId, source.FileId, ct) is { Status: AudioSplitStatus.Pending })
            throw new ConflictException("Ce fichier est déjà en cours de découpage.");

        var targets = command.Parts.Select(p => p.SongId).OfType<Guid>().Distinct().ToList();
        var live = await splits.ListLiveSongIdsAsync(bandId, targets, ct);
        if (targets.Any(id => !live.Contains(id)))
            throw new DomainException("Un morceau choisi n'existe plus, ou est à la corbeille.");

        var now = clock.GetUtcNow();
        var split = AudioSplit.Request(bandId, source.FileId, source.FileName, user.UserId, durationMs,
            [.. command.Parts.Select(p => new AudioSplitPart(p.StartMs, p.EndMs, p.Title, p.SongId))], now);
        await limits.EnsureStorageAsync(bandId, AudioSplitFormats.EstimateBytes(source, split.Parts.Sum(p => (long)p.DurationMs)), ct);

        splits.Add(split);
        jobs.Enqueue(AudioSplitJob.JobType, split.Id.ToString(), now);
        await unitOfWork.SaveChangesAsync(ct);
        return AudioSplitDto.From(split);
    }
}

/// <summary>Le dernier découpage demandé pour un fichier, pour suivre son avancement ; null s'il n'y en a pas.</summary>
public sealed class GetLatestAudioSplitHandler(IAudioSplitRepository splits, IBandContext band)
{
    public async Task<AudioSplitDto?> HandleAsync(Guid songId, Guid versionId, Guid fileId, CancellationToken ct)
    {
        var bandId = band.RequiredBandId;
        if (await splits.GetSourceAsync(bandId, songId, versionId, fileId, ct) is null)
            throw new NotFoundException("SongFile", fileId);
        return await splits.GetLatestAsync(bandId, fileId, ct) is { } split ? AudioSplitDto.From(split) : null;
    }
}

/// <summary>
/// Découpe un fichier (payload : identifiant du découpage) : ffmpeg lit l'original une fois et écrit un fichier par
/// passage ; chacun devient une nouvelle version, d'un morceau existant ou d'un nouveau morceau, au statut Répétition.
/// Les passages sont analysés (#17) et reçoivent leur aperçu (#15) comme un envoi. L'original n'est jamais modifié.
/// </summary>
public sealed class AudioSplitJob(
    IAudioSplitRepository splits, IAudioPreviewRepository sources, IFileStorageService storage, IAudioProcessor processor,
    AudioPreviewQueue previews, FileScanQueue scans, IActivityRepository activity, PlanLimits limits,
    IUnitOfWork unitOfWork, TimeProvider clock) : IBackgroundJobHandler
{
    public const string JobType = "audio.split";

    public string Type => JobType;

    public async Task RunAsync(BackgroundJobContext job, CancellationToken ct)
    {
        var splitId = Guid.Parse(job.Payload);
        var split = await splits.GetAsync(splitId, ct);
        if (split is not { Status: AudioSplitStatus.Pending }) return;          // déjà traité
        var sourceKey = await sources.GetSourceKeyAsync(split.SourceFileId, ct);
        if (sourceKey is null)                                                 // purgé, ou bloqué depuis la demande (#17)
        {
            await MarkFailedAsync(splitId, "Le fichier d'origine n'est plus disponible.");
            return;
        }

        var format = AudioSplitFormats.For(sourceKey);
        var rule = AudioSplitFormats.Rule(format);
        var folder = Path.Combine(Path.GetTempPath(), $"songvault-split-{Guid.NewGuid():N}");
        var storedKeys = new List<string>();
        try
        {
            Directory.CreateDirectory(folder);
            var cuts = split.Parts.Select((p, i) => new AudioCut(p.StartMs, p.DurationMs, Path.Combine(folder, $"{i}{rule.Extension}"))).ToList();
            await using (var source = await storage.OpenReadAsync(sourceKey, ct))
                await processor.SplitAsync(source, cuts, format, ct);

            var sizes = cuts.Select(c => File.Exists(c.OutputPath) ? new FileInfo(c.OutputPath).Length : 0).ToList();
            if (sizes.Any(size => size == 0)) throw new AudioProcessingException("Un passage est vide : il ne contient aucun son.");
            await limits.EnsureStorageAsync(split.BandId, sizes.Sum(), ct);   // #16, avec les tailles réelles

            foreach (var cut in cuts)
            {
                var storageKey = $"{Guid.CreateVersion7():N}{rule.Extension}";   // comme un envoi
                await using var content = File.OpenRead(cut.OutputPath);
                await storage.SaveAsync(storageKey, content, ct);
                storedKeys.Add(storageKey);
            }

            await AttachAsync(split, storedKeys, sizes, rule, ct);
        }
        catch (Exception ex)
        {
            foreach (var storageKey in storedKeys)
                await storage.DeleteAsync(storageKey, CancellationToken.None);     // compensation
            if (ct.IsCancellationRequested) throw;                              // API arrêtée : la tâche reprendra
            // Un refus (morceau parti, stockage plein) ne passera pas mieux au prochain essai : échec tout de suite
            if (ex is DomainException or PlanLimitException)
            {
                await MarkFailedAsync(splitId, ex.Message);
                return;
            }
            if (job.IsLastAttempt)
                await MarkFailedAsync(splitId, ex switch
                {
                    AudioProcessingException audio => audio.UserMessage,
                    OperationCanceledException => "Le découpage a pris trop de temps.",
                    _ => "Erreur technique pendant le découpage.",
                });
            throw;
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }

        await limits.RefreshStorageAsync(split.BandId, ct);                   // jauge (#16), les passages sont enregistrés
    }

    private async Task AttachAsync(AudioSplit split, IReadOnlyList<string> storageKeys, IReadOnlyList<long> sizes,
        FileTypeRule rule, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var songs = new Dictionary<Guid, Song>();
        var created = new List<(Guid SongId, Guid VersionId)>();
        for (var i = 0; i < split.Parts.Count; i++)
        {
            var part = split.Parts[i];
            Song song;
            if (part.SongId is { } songId)
            {
                if (!songs.TryGetValue(songId, out song!))
                    songs[songId] = song = await splits.GetSongAsync(split.BandId, songId, ct)
                        ?? throw new DomainException($"Le morceau choisi pour « {part.Title} » n'existe plus, ou est à la corbeille.");
            }
            else
            {
                song = Song.Create(split.BandId, part.Title, null, null, now);
                splits.AddSong(song);
                activity.Add(ActivityEvent.ForSong(ActivityType.SongCreated, song, split.RequestedById, now));
            }

            var version = song.AddVersion(split.PartVersionTitle(), SongVersionStatus.Rehearsal, split.PartNotes(part), null, now);
            var file = version.AddFile(split.PartFileName(part, rule.Extension), storageKeys[i], rule.ContentType,
                sizes[i], SongFileType.Audio, now);
            previews.RequestFor(file, now);                                    // #15
            scans.RequestFor(file, now);                                       // #17
            activity.Add(ActivityEvent.ForVersion(ActivityType.VersionAdded, song, version, split.RequestedById, now));
            created.Add((song.Id, version.Id));
        }
        split.MarkDone(created, now);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task MarkFailedAsync(Guid splitId, string reason)
    {
        unitOfWork.DiscardChanges();
        var failed = await splits.GetAsync(splitId, CancellationToken.None);
        if (failed is null) return;
        failed.MarkFailed(reason, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(CancellationToken.None);
    }
}
