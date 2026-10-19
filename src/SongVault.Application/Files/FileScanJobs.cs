using System.Net;
using Microsoft.Extensions.Options;
using SongVault.Application.Abstractions;
using SongVault.Application.Common;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Files;
using SongVault.Domain.Audit;

namespace SongVault.Application.Files;

/// <summary>Demande l'analyse antivirus d'un fichier qui vient d'être rattaché à une version (ADR 0024).</summary>
public sealed class FileScanQueue(IFileScanRepository scans, IBackgroundJobs jobs)
{
    /// <summary>
    /// Ajoute l'analyse « en attente » et sa tâche au contexte, sans enregistrer : elles partent avec le fichier,
    /// au même SaveChangesAsync. Le fichier reste illisible jusqu'au verdict.
    /// </summary>
    public FileScanStatus RequestFor(SongFile file, DateTimeOffset now)
    {
        scans.Add(FileScan.Request(file.Id, now));
        jobs.Enqueue(FileScanJob.JobType, file.Id.ToString(), now);
        return FileScanStatus.Pending;
    }
}

/// <summary>
/// Analyse un fichier avec l'antivirus (payload : identifiant du fichier). Sain : il devient lisible et,
/// pour un fichier audio, son aperçu (#15) est lancé. Infecté : il reste en quarantaine et son auteur est prévenu.
/// Antivirus désactivé : le fichier devient lisible sans être lu (NotScanned) et son aperçu est lancé.
/// </summary>
public sealed class FileScanJob(
    IFileScanRepository scans, IAudioPreviewRepository previews, IFileStorageService storage, IMalwareScanner scanner,
        IBackgroundJobs jobs, IUnitOfWork unitOfWork, TimeProvider clock, IAuditRepository audit) : IBackgroundJobHandler
{
    public const string JobType = "file.scan";

    public string Type => JobType;

    public async Task RunAsync(BackgroundJobContext job, CancellationToken ct)
    {
        var fileId = Guid.Parse(job.Payload);
        var scan = await scans.GetAsync(fileId, ct);
        if (scan is not { Status: FileScanStatus.Pending }) return;          // fichier supprimé, ou déjà analysé
        var target = await scans.GetTargetAsync(fileId, ct);
        if (target is null) return;

        if (!scanner.IsEnabled)
        {
            var now = clock.GetUtcNow();
            scan.MarkNotScanned(now);
            await EnqueuePreviewAsync(fileId, target, now, ct);
            await SaveAsync(ct);
            return;
        }

        try
        {
            MalwareScanResult result;
            await using (var content = await storage.OpenReadAsync(target.StorageKey, ct))
                result = await scanner.ScanAsync(content, ct);

            var now = clock.GetUtcNow();
            if (result.Threat is { } threat)
            {
                scan.MarkInfected(threat, now);
                if (target.FileType == SongFileType.Audio)
                    (await previews.GetAsync(fileId, ct))?.MarkFailed("Fichier bloqué par l'antivirus.", now);
                jobs.Enqueue(InfectedFileNoticeJob.JobType, fileId.ToString(), now);
                // #19 : au journal d'audit du groupe, sans auteur (c'est SongVault qui bloque)
                if (target.BandId != Guid.Empty)
                    audit.Add(AuditEntry.Record(target.BandId, null, AuditAction.FileQuarantined, target.Label, now,
                        details: $"menace : {threat}"));
            }
            else
            {
                scan.MarkClean(now);
                await EnqueuePreviewAsync(fileId, target, now, ct);
            }

            await SaveAsync(ct);
        }
        catch (Exception ex) when (job.IsLastAttempt && !ct.IsCancellationRequested)
        {
            unitOfWork.DiscardChanges();
            var failed = await scans.GetAsync(fileId, CancellationToken.None);
            failed?.MarkFailed(ex is MalwareScannerException
                ? "L'antivirus n'a pas pu analyser le fichier." : "Erreur technique pendant l'analyse.", clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    /// <summary>#15 : la conversion d'un fichier audio attend que le fichier soit lisible.</summary>
    private async Task EnqueuePreviewAsync(Guid fileId, FileScanTarget target, DateTimeOffset now, CancellationToken ct)
    {
        if (target.FileType != SongFileType.Audio) return;
        if (await previews.GetAsync(fileId, ct) is { Status: AudioPreviewStatus.Pending })
            jobs.Enqueue(AudioPreviewJob.JobType, fileId.ToString(), now);
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // Fichier supprimé pendant l'analyse : l'analyse est partie avec lui, il n'y a rien à garder
        }
    }
}

/// <summary>
/// Activation de l'antivirus (Antivirus:Provider None → ClamAv) : chaque fichier resté NotScanned repasse
/// en attente et reçoit sa tâche d'analyse. Lancé au démarrage de l'API ; sans effet s'il n'y a plus rien à reprendre.
/// </summary>
public sealed class RescanNotScannedFilesHandler(
    IFileScanRepository scans, IBackgroundJobs jobs, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public const int BatchSize = 200;

    /// <returns>Nombre de fichiers remis en analyse.</returns>
    public async Task<int> HandleAsync(CancellationToken ct)
    {
        var total = 0;
        while (true)
        {
            var batch = await scans.ListNotScannedAsync(BatchSize, ct);
            if (batch.Count == 0) return total;

            var now = clock.GetUtcNow();
            foreach (var scan in batch)
            {
                scan.RequestAfterActivation(now);
                jobs.Enqueue(FileScanJob.JobType, scan.SongFileId.ToString(), now);
            }
            await unitOfWork.SaveChangesAsync(ct);                      // un lot à la fois, chacun avec ses tâches
            total += batch.Count;
        }
    }
}

/// <summary>Prévient par e-mail le compte qui a envoyé un fichier infecté (payload : identifiant du fichier).</summary>
public sealed class InfectedFileNoticeJob(IFileScanRepository scans, IEmailSender emailSender, IOptions<AppOptions> app)
    : IBackgroundJobHandler
{
    public const string JobType = "file.infected-notice";

    public string Type => JobType;

    public async Task RunAsync(BackgroundJobContext job, CancellationToken ct)
    {
        if (await scans.GetInfectedNoticeAsync(Guid.Parse(job.Payload), ct) is not { } notice) return;
        var link = $"{app.Value.PublicUrl.TrimEnd('/')}/b/{notice.BandId}/songs/{notice.SongId}/versions/{notice.VersionId}";
        await emailSender.SendAsync(FileScanEmails.Infected(notice, link), ct);
    }
}

/// <summary>Relance une analyse en échec, à la demande d'un membre (« Relancer l'analyse »).</summary>
public sealed class RetryFileScanHandler(
    ISongRepository songs, IFileScanRepository scans, IBackgroundJobs jobs, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<FileScanStatus> HandleAsync(Guid songId, Guid versionId, Guid fileId, CancellationToken ct)
    {
        _ = await songs.GetFileLabelAsync(songId, versionId, fileId, ct)          // dans le groupe actif seulement
            ?? throw new NotFoundException("SongFile", fileId);
        var scan = await scans.GetAsync(fileId, ct) ?? throw new NotFoundException("FileScan", fileId);
        if (scan.Status != FileScanStatus.Failed)
            throw new ConflictException(scan.Status switch
            {
                FileScanStatus.Pending => "L'analyse est déjà en cours.",
                FileScanStatus.Clean => "Le fichier a déjà été analysé : il est sain.",
                FileScanStatus.NotScanned => "L'antivirus n'est pas encore activé : le fichier sera analysé à son activation.",
                _ => "Un fichier infecté n'est pas analysé de nouveau : supprimez-le.",
            });

        var now = clock.GetUtcNow();
        scan.Retry(now);
        jobs.Enqueue(FileScanJob.JobType, fileId.ToString(), now);
        await unitOfWork.SaveChangesAsync(ct);
        return FileScanStatus.Pending;
    }
}

/// <summary>
/// La seule porte vers le contenu d'un fichier : il n'est rendu qu'une fois déclaré sain, ou marqué
/// non analysé tant que l'antivirus est désactivé (ADR 0024).
/// </summary>
public static class FileScanGate
{
    /// <param name="status">null : pas d'analyse connue, traité comme une analyse en cours.</param>
    /// <exception cref="FileNotReadableException">Le fichier n'est pas déclaré sain.</exception>
    public static void EnsureReadable(FileScanStatus? status)
    {
        if (status is FileScanStatus.Clean or FileScanStatus.NotScanned) return;
        throw new FileNotReadableException(status switch
        {
            FileScanStatus.Infected => "Fichier bloqué : l'antivirus y a détecté une menace.",
            FileScanStatus.Failed => "L'analyse antivirus de ce fichier a échoué : relancez-la depuis la version.",
            _ => "Analyse antivirus en cours : le fichier sera disponible dans un instant.",
        });
    }
}

public static class FileScanEmails
{
    public static EmailMessage Infected(InfectedFileNotice n, string link)
    {
        var subject = $"Fichier bloqué par l'antivirus dans « {n.BandName} »";
        var text = $"""
            L'antivirus de SongVault a détecté une menace dans le fichier « {n.FileName} » que vous avez envoyé
            sur la v{n.VersionNumber} de « {n.SongTitle} » (groupe « {n.BandName} »).

            Menace détectée : {n.Threat}

            Le fichier est bloqué : personne ne peut l'écouter, le télécharger ni le partager. Vous pouvez le supprimer
            depuis la version : {link}

            Si le fichier vient de votre ordinateur, analysez celui-ci avec votre antivirus.
            """;

        Func<string, string> h = s => WebUtility.HtmlEncode(s);
        var html = $"""
            <p>L'antivirus de SongVault a détecté une menace dans le fichier « {h(n.FileName)} » que vous avez envoyé
            sur la v{n.VersionNumber} de « {h(n.SongTitle)} » (groupe « {h(n.BandName)} »).</p>
            <p>Menace détectée : <strong>{h(n.Threat)}</strong></p>
            <p>Le fichier est bloqué : personne ne peut l'écouter, le télécharger ni le partager.
            <a href="{h(link)}">Ouvrir la version</a> pour le supprimer.</p>
            <p style="color:#666">Si le fichier vient de votre ordinateur, analysez celui-ci avec votre antivirus.</p>
            """;

        return new EmailMessage(n.UploaderEmail, subject, html, text);
    }
}