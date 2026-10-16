using SongVault.Application.Abstractions;

namespace SongVault.Application.Accounts;

/// <summary>Efface un compte supprimé depuis 30 jours (payload : identifiant du compte).</summary>
public sealed class PurgeAccountJob(
    IAccountRepository accounts, IFileStorageService storage, IBillingRepository billing, IPaymentGateway payments)
    : IBackgroundJobHandler
{
    public const string JobType = "account.purge";

    public string Type => JobType;

    public async Task RunAsync(BackgroundJobContext job, CancellationToken ct)
    {
        // #16 : un abonnement résilié pour la fin de la période, qui court encore, s'arrête avec le groupe effacé.
        // Avant la base : la purge efface les lignes d'abonnement. Une erreur de Stripe fait réessayer la tâche.
        foreach (var subscriptionId in await billing.ListStripeSubscriptionsToCancelAsync(job.Payload, ct))
            await payments.CancelSubscriptionAsync(subscriptionId, ct);

        var keys = await accounts.PurgeAsync(job.Payload, ct);  // la base d'abord, en une transaction

        // Un fichier qui résiste reste orphelin : scripts/purge-orphan-files.ps1 le retrouve
        foreach (var key in keys)
            await storage.DeleteAsync(key, CancellationToken.None);
    }
}