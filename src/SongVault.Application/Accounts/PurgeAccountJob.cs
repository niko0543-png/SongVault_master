using SongVault.Application.Abstractions;

namespace SongVault.Application.Accounts;

/// <summary>Efface un compte supprimé depuis 30 jours (payload : identifiant du compte).</summary>
public sealed class PurgeAccountJob(IAccountRepository accounts, IFileStorageService storage) : IBackgroundJobHandler
{
    public const string JobType = "account.purge";

    public string Type => JobType;

    public async Task RunAsync(BackgroundJobContext job, CancellationToken ct)
    {
        var keys = await accounts.PurgeAsync(job.Payload, ct);  // la base d'abord, en une transaction

        // Un fichier qui résiste reste orphelin : scripts/purge-orphan-files.ps1 le retrouve
        foreach (var key in keys)
            await storage.DeleteAsync(key, CancellationToken.None);
    }
}