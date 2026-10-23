using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;
using SongVault.Application.Files;
using SongVault.Domain.Releases;

namespace SongVault.Application.Releases.Exports;

public sealed record ReleaseExportDto(
    Guid Id, ReleaseExportStatus Status, DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt, DateTimeOffset? ExpiresAt, long? SizeBytes)
{
    public static ReleaseExportDto From(ReleaseExport e)
        => new(e.Id, e.Status, e.RequestedAt, e.CompletedAt, e.ExpiresAt, e.SizeBytes);
}

public sealed class RequestReleaseExportHandler(
    IReleaseExportRepository exports, IBackgroundJobs jobs, IUnitOfWork unitOfWork, IBandContext bandContext,
    ICurrentUser currentUser, TimeProvider clock)
{
    /// <summary>
    /// Demande l'archive d'une sortie. Refusé (409) si une archive est déjà en préparation, ou si l'archive ne serait
    /// pas complète (sans piste, piste sans audio, fichier pas encore lisible).
    /// </summary>
    public async Task<ReleaseExportDto> HandleAsync(Guid releaseId, CancellationToken ct)
    {
        var bandId = bandContext.RequiredBandId;
        var source = await exports.ReadSourceAsync(bandId, releaseId, ct) ?? throw new NotFoundException("Release", releaseId);
        if (await exports.GetLatestAsync(bandId, releaseId, ct) is { Status: ReleaseExportStatus.Pending })
            throw new ConflictException("Une archive de cette sortie est déjà en préparation : un e-mail vous préviendra quand elle sera prête.");
        ReleaseArchive.EnsureReady(source);

        var now = clock.GetUtcNow();
        var export = ReleaseExport.Request(bandId, releaseId, currentUser.UserId, now);
        exports.Add(export);
        jobs.Enqueue(ExportReleaseJob.JobType, export.Id.ToString(), now);
        await unitOfWork.SaveChangesAsync(ct);                  // l'archive et sa tâche, ensemble
        return ReleaseExportDto.From(export);
    }
}

public sealed class GetLatestReleaseExportHandler(IReleaseExportRepository exports, IBandContext bandContext, TimeProvider clock)
{
    /// <summary>Dernière archive de la sortie ; null s'il n'y en a pas, ou si elle a expiré.</summary>
    public async Task<ReleaseExportDto?> HandleAsync(Guid releaseId, CancellationToken ct)
    {
        var export = await exports.GetLatestAsync(bandContext.RequiredBandId, releaseId, ct);
        if (export is null || export.IsExpiredAt(clock.GetUtcNow())) return null;
        return ReleaseExportDto.From(export);
    }
}

public sealed class DownloadReleaseExportHandler(
    IReleaseExportRepository exports, IFileStorageService storage, IBandContext bandContext, TimeProvider clock)
{
    /// <summary>
    /// L'archive, si elle est prête : redirection vers une adresse de lecture courte (stockage objet) ou contenu.
    /// 410 une fois les 24 h passées, 404 si elle n'existe pas, plus, ou pas encore.
    /// </summary>
    public async Task<FileDownload> HandleAsync(Guid releaseId, Guid exportId, CancellationToken ct)
    {
        var bandId = bandContext.RequiredBandId;
        var export = await exports.GetAsync(exportId, ct);
        if (export is null || export.BandId != bandId || export.ReleaseId != releaseId)
            throw new NotFoundException("ReleaseExport", exportId);
        if (export.IsExpiredAt(clock.GetUtcNow()))
            throw new LinkExpiredException("Ce lien a expiré : préparez une nouvelle archive depuis la page de la sortie.");
        if (!export.IsDownloadableAt(clock.GetUtcNow()))
            throw new NotFoundException("ReleaseExport", exportId);

        var title = await exports.GetReleaseTitleAsync(bandId, releaseId, ct) ?? throw new NotFoundException("Release", releaseId);
        var fileName = ReleaseArchive.FileName(title);
        var url = storage.CreateDownloadUrl(export.StorageKey, ReleaseArchive.ContentType, fileName);
        if (url is not null) return new FileDownload(null, ReleaseArchive.ContentType, fileName, url);

        return new FileDownload(await storage.OpenReadAsync(export.StorageKey, ct), ReleaseArchive.ContentType, fileName);
    }
}