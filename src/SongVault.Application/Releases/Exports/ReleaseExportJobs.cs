using Microsoft.Extensions.Options;

using SongVault.Application.Abstractions;
using SongVault.Application.Common;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Releases;

namespace SongVault.Application.Releases.Exports;

/// <summary>Écrit l'archive d'une sortie dans le stockage, en flux (payload : identifiant de l'archive).</summary>
public sealed class ExportReleaseJob(
    IReleaseExportRepository exports, IFileStorageService storage, IBackgroundJobs jobs, IUnitOfWork unitOfWork,
    INotificationRepository notifications, IEmailSender emailSender, IOptions<AppOptions> app, TimeProvider clock)
    : IBackgroundJobHandler
{
    public const string JobType = "release.export";

    public string Type => JobType;

    public async Task RunAsync(BackgroundJobContext job, CancellationToken ct)
    {
        var exportId = Guid.Parse(job.Payload);
        var export = await exports.GetAsync(exportId, ct);
        if (export is not { Status: ReleaseExportStatus.Pending }) return;   // partie avec sa sortie, ou déjà traitée

        ReleaseArchiveSource? source;
        try
        {
            source = await exports.ReadSourceAsync(export.BandId, export.ReleaseId, ct);
            if (source is null) return;
            ReleaseArchive.EnsureReady(source);                  // un fichier a pu être bloqué depuis la demande
            await WriteArchiveAsync(export, source, ct);
        }
        catch (Exception)
        {
            await storage.DeleteAsync(export.StorageKey, CancellationToken.None);   // jamais d'archive à moitié écrite
            if (job.IsLastAttempt) await MarkFailedAsync(exportId);
            throw;
        }

        var to = await notifications.GetEmailAsync(export.RequestedById, ct);
        if (to is null) return;                                  // compte supprimé entre-temps
        try
        {
            var link = $"{app.Value.PublicUrl.TrimEnd('/')}/b/{export.BandId}/releases/{export.ReleaseId}";
            await emailSender.SendAsync(ReleaseExportEmails.Ready(to, source.Release.Title, link, export.ExpiresAt!.Value), ct);
        }
        catch (EmailDeliveryException)
        {
            // L'archive est sur la page de la sortie : l'e-mail n'est qu'un rappel (détail dans les journaux)
        }
    }

    private async Task WriteArchiveAsync(ReleaseExport export, ReleaseArchiveSource source, CancellationToken ct)
    {
        await storage.DeleteAsync(export.StorageKey, ct);      // reste d'un essai précédent
        await using (var output = await storage.OpenWriteAsync(export.StorageKey, ct))
            await ReleaseArchiveWriter.WriteAsync(output, ReleaseArchive.Plan(source), clock.GetUtcNow(), storage.OpenReadAsync, ct);

        var size = await storage.GetLengthAsync(export.StorageKey, ct)
                   ?? throw new InvalidOperationException("Archive absente du stockage après son écriture.");
        export.MarkReady(size, clock.GetUtcNow());
        jobs.Enqueue(ExpireReleaseExportJob.JobType, export.Id.ToString(), export.ExpiresAt!.Value);
        await unitOfWork.SaveChangesAsync(ct);
    }

    // Dernier essai manqué : l'échec se voit sur la page de la sortie, et la ligne part au bout de 24 h
    private async Task MarkFailedAsync(Guid exportId)
    {
        unitOfWork.DiscardChanges();
        var failed = await exports.GetAsync(exportId, CancellationToken.None);
        if (failed is null) return;
        var now = clock.GetUtcNow();
        failed.MarkFailed(now);
        jobs.Enqueue(ExpireReleaseExportJob.JobType, exportId.ToString(), now + ReleaseExport.Lifetime);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);
    }
}

/// <summary>
/// Efface une archive arrivée à expiration (payload : identifiant de l'archive). La clé se déduit de l'identifiant :
/// le fichier part aussi quand la ligne est déjà partie avec sa sortie.
/// </summary>
public sealed class ExpireReleaseExportJob(IReleaseExportRepository exports, IFileStorageService storage, IUnitOfWork unitOfWork)
    : IBackgroundJobHandler
{
    public const string JobType = "release.export.expire";

    public string Type => JobType;

    public async Task RunAsync(BackgroundJobContext job, CancellationToken ct)
    {
        var exportId = Guid.Parse(job.Payload);
        var export = await exports.GetAsync(exportId, ct);
        if (export is not null)
        {
            exports.Remove(export);
            await unitOfWork.SaveChangesAsync(ct);
        }
        await storage.DeleteAsync(ReleaseExport.StorageKeyFor(exportId), CancellationToken.None);
    }
}