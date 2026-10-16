using SongVault.Domain.Billing;

namespace SongVault.Application.Billing;

public sealed record PlanDto(string Id, string Name, long StorageQuotaBytes, int? MaxBandsPerOwner, int MonthlyPriceCents)
{
    public static PlanDto From(Plan plan)
        => new(plan.Id, plan.Name, plan.StorageQuotaBytes, plan.MaxBandsPerOwner, plan.MonthlyPriceCents);
}

/// <summary>Les formules, et si on peut les payer en ligne.</summary>
public sealed record PlanCatalogDto(bool OnlinePayment, IReadOnlyList<PlanDto> Plans);

/// <summary>Page Facturation : formule, état de l'abonnement et actions possibles.</summary>
/// <param name="Status">null : jamais d'abonnement (formule gratuite).</param>
/// <param name="IsGranted">Formule offerte, sans paiement.</param>
/// <param name="CanCheckout">Un paiement peut commencer : paiement ouvert et aucun abonnement Stripe en cours.</param>
/// <param name="CanOpenPortal">Le portail client existe : un paiement a déjà eu lieu pour ce groupe.</param>
public sealed record BillingDto(
    BandUsageDto Usage, SubscriptionStatus? Status, DateTimeOffset? CurrentPeriodEnd, bool CancelAtPeriodEnd,
    bool IsGranted, bool CanCheckout, bool CanOpenPortal, bool OnlinePayment);