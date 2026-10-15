using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Files;

namespace SongVault.Application.Files;

/// <summary>Le fichier à écouter : le MP3 d'aperçu s'il est prêt, sinon l'original (ADR 0021).</summary>
public sealed class ListenSongFileHandler(ISongRepository songs, IFileStorageService storage)
{
    public async Task<FileDownload> HandleAsync(Guid songId, Guid versionId, Guid fileId, CancellationToken ct)
    {
        var info = await songs.GetListenFileAsync(songId, versionId, fileId, ct)
                   ?? throw new NotFoundException("SongFile", fileId);

        var url = storage.CreateDownloadUrl(info.StorageKey, info.ContentType, null);
        if (url is not null) return new FileDownload(null, info.ContentType, info.OriginalFileName, url);
        return new FileDownload(await storage.OpenReadAsync(info.StorageKey, ct), info.ContentType, info.OriginalFileName);
    }
}

public sealed class GetWaveformHandler(ISongRepository songs)
{
    /// <exception cref="NotFoundException">Fichier inconnu, pas audio, ou aperçu pas encore prêt.</exception>
    public async Task<AudioWaveformDto> HandleAsync(Guid songId, Guid versionId, Guid fileId, CancellationToken ct)
        => await songs.GetWaveformAsync(songId, versionId, fileId, ct) ?? throw new NotFoundException("AudioWaveform", fileId);
}

/// <summary>Relance un aperçu en échec, à la demande d'un membre (« Réessayer »).</summary>
public sealed class RetryAudioPreviewHandler(
    ISongRepository songs, IAudioPreviewRepository previews, IBackgroundJobs jobs, IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<AudioPreviewStatus> HandleAsync(Guid songId, Guid versionId, Guid fileId, CancellationToken ct)
    {
        var file = await songs.GetFileLabelAsync(songId, versionId, fileId, ct);     // dans le groupe actif seulement
        if (file is not { FileType: SongFileType.Audio }) throw new NotFoundException("SongFile", fileId);

        var now = clock.GetUtcNow();
        var preview = await previews.GetAsync(fileId, ct);
        if (preview is null)                                   // filet de sécurité : la migration en crée un par fichier
            previews.Add(AudioPreview.Request(fileId, now));
        else if (preview.Status != AudioPreviewStatus.Failed)
            throw new ConflictException(preview.Status == AudioPreviewStatus.Pending
                ? "L'aperçu est déjà en préparation." : "L'aperçu est déjà prêt.");
        else
            preview.Retry(now);

        jobs.Enqueue(AudioPreviewJob.JobType, fileId.ToString(), now);
        await unitOfWork.SaveChangesAsync(ct);
        return AudioPreviewStatus.Pending;
    }
}