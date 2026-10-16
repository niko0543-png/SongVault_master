using SongVault.Domain.Billing;

namespace SongVault.Application.Abstractions;

/// <summary>Le groupe, son nom et les adresses de ses Owner (e-mail de paiement refusé).</summary>
public sealed record BandOwners(string BandName, IReadOnlyList<string> Emails);

/// <summary>Formules, abonnements et stockage utilisé des groupes (ADR 0023).</summary>
public interface IBillingRepository
{
    /// <summary>Toutes les formules, dans l'ordre d'affichage.</summary>
    Task<IReadOnlyList<Plan>> ListPlansAsync(CancellationToken ct);
    Task<Plan?> GetPlanAsync(string planId, CancellationToken ct);

    /// <summary>Abonnement du groupe, suivi par EF ; null : formule gratuite.</summary>
    Task<Subscription?> GetSubscriptionAsync(Guid bandId, CancellationToken ct);
    void AddSubscription(Subscription subscription);
    /// <summary>Abonnements de ces groupes, suivis par EF.</summary>
    Task<IReadOnlyList<Subscription>> ListSubscriptionsAsync(IReadOnlyCollection<Guid> bandIds, CancellationToken ct);
    /// <summary>
    /// Abonnements Stripe qui courent encore sur les groupes dont ce compte, désactivé, est le seul membre :
    /// ceux que la purge du compte doit arrêter. Vide si le compte n'existe plus ou a été réactivé.
    /// </summary>
    Task<IReadOnlyList<string>> ListStripeSubscriptionsToCancelAsync(string userId, CancellationToken ct);

    /// <summary>Octets utilisés par le groupe (Band.StorageUsedBytes) ; null si le groupe n'existe pas.</summary>
    Task<long?> GetStorageUsedAsync(Guid bandId, CancellationToken ct);
    /// <summary>Recalcule le stockage utilisé du groupe en une instruction SQL, sur les fichiers enregistrés.</summary>
    Task RefreshStorageUsedAsync(Guid bandId, CancellationToken ct);
    /// <summary>Même recalcul pour tous les groupes (tâche de nuit) ; renvoie le nombre de groupes.</summary>
    Task<int> RefreshAllStorageUsedAsync(CancellationToken ct);

    /// <summary>Groupes dont ce compte est Owner et qui n'ont pas d'abonnement en cours.</summary>
    Task<int> CountOwnedBandsOnFreePlanAsync(string userId, CancellationToken ct);
    Task<bool> BandExistsAsync(Guid bandId, CancellationToken ct);
    Task<BandOwners?> GetBandOwnersAsync(Guid bandId, CancellationToken ct);

    Task<bool> IsEventProcessedAsync(string eventId, CancellationToken ct);
    void AddProcessedEvent(ProcessedStripeEvent processed);
}