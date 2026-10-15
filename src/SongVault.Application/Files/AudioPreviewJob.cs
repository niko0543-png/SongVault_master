using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Files;

namespace SongVault.Application.Files;

/// <summary>Demande l'aperçu d'un fichier qui vient d'être rattaché à une version (ADR 0021).</summary>
public sealed class AudioPreviewQueue(IAudioPreviewRepository previews, IBackgroundJobs jobs)
{
    /// <summary>
    /// Fichier audio : ajoute l'aperçu « en préparation » et sa tâche au contexte, sans enregistrer
    /// (ils partent avec le fichier, au même SaveChangesAsync). Autre fichier : rien, et null.
    /// </summary>
    public AudioPreviewStatus? RequestFor(SongFile file, DateTimeOffset now)
    {
        if (file.FileType != SongFileType.Audio) return null;
        previews.Add(AudioPreview.Request(file.Id, now));
        jobs.Enqueue(AudioPreviewJob.JobType, file.Id.ToString(), now);
        return AudioPreviewStatus.Pending;
    }
}

/// <summary>
/// Prépare l'aperçu d'un fichier audio (payload : identifiant du fichier) : forme d'onde, durée et,
/// sauf pour un MP3, un MP3 à 128 kbit/s pour l'écoute. L'original est lu, jamais modifié.
/// </summary>
public sealed class AudioPreviewJob(
    IAudioPreviewRepository previews, IFileStorageService storage, IAudioProcessor processor, IUnitOfWork unitOfWork,
    TimeProvider clock) : IBackgroundJobHandler
{
    public const string JobType = "audio.preview";

    public string Type => JobType;

    public async Task RunAsync(BackgroundJobContext job, CancellationToken ct)
    {
        var fileId = Guid.Parse(job.Payload);
        var preview = await previews.GetAsync(fileId, ct);
        if (preview is not { Status: AudioPreviewStatus.Pending }) return;      // fichier supprimé, ou déjà traité
        var sourceKey = await previews.GetSourceKeyAsync(fileId, ct);
        if (sourceKey is null) return;

        var needsMp3 = !sourceKey.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);
        var mp3Path = needsMp3 ? Path.Combine(Path.GetTempPath(), $"songvault-{Guid.NewGuid():N}.mp3") : null;
        try
        {
            AudioAnalysis analysis;
            await using (var source = await storage.OpenReadAsync(sourceKey, ct))
                analysis = await processor.ProcessAsync(source, mp3Path, ct);

            string? previewKey = null;
            if (mp3Path is not null)
            {
                previewKey = AudioPreview.StorageKeyFor(fileId);
                await storage.DeleteAsync(previewKey, ct);                     // reste d'un essai précédent
                await using var mp3 = File.OpenRead(mp3Path);
                await storage.SaveAsync(previewKey, mp3, ct);
            }

            preview.MarkReady(previewKey, analysis.Peaks, analysis.DurationMs, clock.GetUtcNow());
            try
            {
                await unitOfWork.SaveChangesAsync(ct);
            }
            catch (ConcurrencyConflictException)
            {
                // Fichier supprimé pendant la conversion : l'aperçu est parti avec lui, son MP3 suit
                if (previewKey is not null) await storage.DeleteAsync(previewKey, CancellationToken.None);
            }
        }
        catch (Exception ex) when (job.IsLastAttempt && !ct.IsCancellationRequested)
        {
            unitOfWork.DiscardChanges();
            var failed = await previews.GetAsync(fileId, CancellationToken.None);
            failed?.MarkFailed(ex is AudioProcessingException audio ? audio.UserMessage
                : ex is OperationCanceledException ? "La conversion a pris trop de temps."
                : "Erreur technique pendant la conversion.", clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            if (mp3Path is not null) File.Delete(mp3Path);                   // ne fait rien s'il n'existe pas
        }
    }
}