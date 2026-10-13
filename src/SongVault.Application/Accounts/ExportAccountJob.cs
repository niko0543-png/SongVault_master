using Microsoft.Extensions.Options;

using SongVault.Application.Abstractions;
using SongVault.Application.Common;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Accounts;

namespace SongVault.Application.Accounts;

/// <summary>Prépare l'archive d'un export (payload : identifiant de l'export).</summary>
public sealed class ExportAccountJob(
    IAccountRepository accounts, IFileStorageService storage, IBackgroundJobs jobs, IUnitOfWork unitOfWork,
    IEmailSender emailSender, IOptions<AppOptions> app, TimeProvider clock) : IBackgroundJobHandler
{
    public const string JobType = "account.export";

    public string Type => JobType;

    public async Task RunAsync(BackgroundJobContext job, CancellationToken ct)
    {
        var exportId = Guid.Parse(job.Payload);
        var export = await accounts.GetExportAsync(exportId, ct);
        if (export is not { Status: DataExportStatus.Pending }) return;      // effacé avec le compte, ou déjà traité

        AccountData? data;
        try
        {
            data = await accounts.ReadAccountDataAsync(export.UserId, clock.GetUtcNow(), ct);
            if (data is null) return;
            await WriteArchiveAsync(export, data, ct);
        }
        catch (Exception) when (job.IsLastAttempt)
        {
            unitOfWork.DiscardChanges();
            var failed = await accounts.GetExportAsync(exportId, CancellationToken.None);
            failed?.MarkFailed(clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        try
        {
            await emailSender.SendAsync(AccountEmails.ExportReady(
                data.Email, $"{app.Value.PublicUrl.TrimEnd('/')}/account", export.ExpiresAt!.Value), ct);
        }
        catch (EmailDeliveryException)
        {
            // L'archive est dans « Mon compte » : l'e-mail n'est qu'un rappel (détail dans les journaux)
        }
    }

    private async Task WriteArchiveAsync(DataExport export, AccountData data, CancellationToken ct)
    {
        var key = $"export-{export.Id:N}.zip";
        var tempPath = Path.Combine(Path.GetTempPath(), $"songvault-{Guid.NewGuid():N}.zip");
        await using var temp = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);

        await AccountExportWriter.WriteAsync(temp, data, clock.GetUtcNow(), storage.OpenReadAsync, ct);
        var size = temp.Length;
        temp.Position = 0;

        await storage.DeleteAsync(key, ct);                     // reste d'un essai précédent
        await storage.SaveAsync(key, temp, ct);

        export.MarkReady(key, size, clock.GetUtcNow());
        jobs.Enqueue(ExpireExportJob.JobType, export.Id.ToString(), export.ExpiresAt!.Value);
        await unitOfWork.SaveChangesAsync(ct);
    }
}