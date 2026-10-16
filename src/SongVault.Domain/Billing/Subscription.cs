using SongVault.Domain.Common;

namespace SongVault.Domain.Billing;

/// <summary>États d'un abonnement Stripe, plus Active pour une formule offerte (sans Stripe).</summary>
public enum SubscriptionStatus { Active, Trialing, PastDue, Incomplete, IncompleteExpired, Canceled, Unpaid, Paused }

/// <summary>Un abonnement tel que Stripe le décrit, lu juste avant d'être enregistré.</summary>
/// <param name="BandId">Groupe noté dans les métadonnées de l'abonnement (band_id) ; null s'il manque.</param>
/// <param name="PlanId">Formule dont le prix est celui de l'abonnement ; null si le prix n'est pas configuré.</param>
public sealed record SubscriptionState(
    string SubscriptionId, string CustomerId, Guid? BandId, string? PlanId, SubscriptionStatus Status,
    DateTimeOffset? CurrentPeriodEnd, bool CancelAtPeriodEnd);

/// <summary>
/// L'abonnement d'un groupe (ADR 0023) : porté par le groupe, géré par ses Owner. Sans ligne, le groupe est
/// sur la formule gratuite. Une ligne sans identifiant Stripe est une formule offerte, sans fin.
/// </summary>
public sealed class Subscription
{
    public const int StripeIdMaxLength = 255;

    private Subscription() { } // réservé à EF Core

    public Guid BandId { get; private set; }
    public string PlanId { get; private set; } = default!;
    public SubscriptionStatus Status { get; private set; }
    public string? StripeCustomerId { get; private set; }
    public string? StripeSubscriptionId { get; private set; }
    public DateTimeOffset? CurrentPeriodEnd { get; private set; }
    /// <summary>Résiliation demandée : l'abonnement court jusqu'à CurrentPeriodEnd, puis s'arrête.</summary>
    public bool CancelAtPeriodEnd { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// La formule s'applique : abonnement payé, à l'essai, ou en retard de paiement (Stripe réessaie la carte
    /// pendant quelques jours avant d'arrêter l'abonnement).
    /// </summary>
    public bool IsLive => IsLiveStatus(Status);

    /// <summary>Formule qui s'applique au groupe : la gratuite dès que l'abonnement est arrêté.</summary>
    public string EffectivePlanId => IsLive ? PlanId : Plan.FreeId;

    /// <summary>Un abonnement Stripe court : on en change par le portail, pas par un nouveau paiement.</summary>
    public bool IsLiveOnStripe => IsLive && StripeSubscriptionId is not null;

    /// <summary>Formule offerte, sans paiement (groupe de test, ami, bêta) : s'arrête seulement si on la retire.</summary>
    public static Subscription Grant(Guid bandId, string planId, DateTimeOffset now)
    {
        if (bandId == Guid.Empty) throw new DomainException("Un abonnement appartient à un groupe.");
        if (string.IsNullOrWhiteSpace(planId) || planId == Plan.FreeId)
            throw new DomainException("Choisissez une formule payante à offrir.");
        return new Subscription { BandId = bandId, PlanId = planId, Status = SubscriptionStatus.Active, UpdatedAt = now };
    }

    public static Subscription FromStripe(Guid bandId, SubscriptionState state, DateTimeOffset now)
    {
        if (bandId == Guid.Empty) throw new DomainException("Un abonnement appartient à un groupe.");
        var subscription = new Subscription { BandId = bandId };
        subscription.Apply(state, now);
        return subscription;
    }

    /// <summary>
    /// Enregistre l'état lu chez Stripe. Renvoie false sans rien changer quand l'état vient d'un ancien abonnement
    /// arrêté alors qu'un autre court : un événement en retard ne doit pas faire perdre la formule payée.
    /// </summary>
    public bool Apply(SubscriptionState state, DateTimeOffset now)
    {
        if (state.PlanId is null)
            throw new DomainException($"Le prix de l'abonnement {state.SubscriptionId} ne correspond à aucune formule.");

        var isOther = StripeSubscriptionId is not null && StripeSubscriptionId != state.SubscriptionId;
        if (isOther && IsLive && !IsLiveStatus(state.Status)) return false;

        PlanId = state.PlanId;
        Status = state.Status;
        StripeCustomerId = state.CustomerId;
        StripeSubscriptionId = state.SubscriptionId;
        CurrentPeriodEnd = state.CurrentPeriodEnd;
        CancelAtPeriodEnd = state.CancelAtPeriodEnd;
        UpdatedAt = now;
        return true;
    }

    public static bool IsLiveStatus(SubscriptionStatus status)
        => status is SubscriptionStatus.Active or SubscriptionStatus.Trialing or SubscriptionStatus.PastDue;
}