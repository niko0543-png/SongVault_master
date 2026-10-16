using System.ComponentModel.DataAnnotations;

using SongVault.Application.Billing;
using SongVault.Domain.Billing;

namespace SongVault.Api.Contracts.Billing;

/// <summary>Une formule ; maxBandsPerOwner null : sans limite de groupes.</summary>
public sealed record PlanResponse(string Id, string Name, long StorageQuotaBytes, int? MaxBandsPerOwner, int MonthlyPriceCents)
{
    public static PlanResponse From(PlanDto dto)
        => new(dto.Id, dto.Name, dto.StorageQuotaBytes, dto.MaxBandsPerOwner, dto.MonthlyPriceCents);
}

/// <summary>Les formules ; onlinePayment faux : on ne peut pas encore payer en ligne.</summary>
public sealed record PlanCatalogResponse(bool OnlinePayment, IReadOnlyList<PlanResponse> Plans)
{
    public static PlanCatalogResponse From(PlanCatalogDto dto) => new(dto.OnlinePayment, [.. dto.Plans.Select(PlanResponse.From)]);
}

/// <summary>Jauge de stockage du groupe.</summary>
public sealed record UsageResponse(string PlanId, string PlanName, long UsedBytes, long QuotaBytes)
{
    public static UsageResponse From(BandUsageDto dto) => new(dto.PlanId, dto.PlanName, dto.UsedBytes, dto.QuotaBytes);
}

/// <summary>Page Facturation ; status null : jamais d'abonnement.</summary>
public sealed record BillingResponse(
    UsageResponse Usage, SubscriptionStatus? Status, DateTimeOffset? CurrentPeriodEnd, bool CancelAtPeriodEnd,
    bool IsGranted, bool CanCheckout, bool CanOpenPortal, bool OnlinePayment)
{
    public static BillingResponse From(BillingDto dto)
        => new(UsageResponse.From(dto.Usage), dto.Status, dto.CurrentPeriodEnd, dto.CancelAtPeriodEnd,
            dto.IsGranted, dto.CanCheckout, dto.CanOpenPortal, dto.OnlinePayment);
}

/// <summary>La formule à payer.</summary>
public sealed record StartCheckoutRequest([Required, MaxLength(Plan.IdMaxLength)] string PlanId);

/// <summary>Page Stripe où envoyer le navigateur.</summary>
public sealed record RedirectResponse(Uri Url);