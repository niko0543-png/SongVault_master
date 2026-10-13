using SongVault.Application.Abstractions;

namespace SongVault.Application.Accounts;

/// <summary>Efface un export arrivé à expiration, et son archive (payload : identifiant de l'export).</summary>
public sealed class ExpireExportJob(IAccountRepository accounts, IFileStorageService storage, IUnitOfWork unitOfWork)
    : IBackgroundJobHandler
{
    public const string JobType = "account.export.expire";

    public string Type => JobType;

    public async Task RunAsync(BackgroundJobContext job, CancellationToken ct)
    {
        var export = await accounts.GetExportAsync(Guid.Parse(job.Payload), ct);
        if (export is null) return;                             // déjà effacé avec le compte

        accounts.RemoveExport(export);
        await unitOfWork.SaveChangesAsync(ct);
        if (export.StorageKey is not null)
            await storage.DeleteAsync(export.StorageKey, CancellationToken.None);
    }
}