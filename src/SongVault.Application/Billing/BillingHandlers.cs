using Microsoft.Extensions.Options;

using SongVault.Application.Abstractions;
using SongVault.Application.Common;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Common;

namespace SongVault.Application.Billing;

/// <summary>Les formules, lisibles sans compte (page Formules).</summary>
public sealed class ListPlansHandler(IBillingRepository billing, IPaymentGateway payments)
{
    public async Task<PlanCatalogDto> HandleAsync(CancellationToken ct)
        => new(payments.IsEnabled, [.. (await billing.ListPlansAsync(ct)).Select(PlanDto.From)]);
}

/// <summary>Jauge de stockage du groupe actif, pour tous ses membres.</summary>
public sealed class GetBandUsageHandler(PlanLimits limits, IBandContext band)
{
    public Task<BandUsageDto> HandleAsync(CancellationToken ct) => limits.GetUsageAsync(band.RequiredBandId, ct);
}

/// <summary>Page Facturation du groupe actif (Owner).</summary>
public sealed class GetBillingHandler(
    IBillingRepository billing, PlanLimits limits, IPaymentGateway payments, IBandContext band)
{
    public async Task<BillingDto> HandleAsync(CancellationToken ct)
    {
        var usage = await limits.GetUsageAsync(band.RequiredBandId, ct);
        var subscription = await billing.GetSubscriptionAsync(band.RequiredBandId, ct);
        return new BillingDto(
            usage,
            subscription?.Status,
            subscription?.CurrentPeriodEnd,
            subscription?.CancelAtPeriodEnd ?? false,
            IsGranted: subscription is { IsLive: true, StripeSubscriptionId: null },
            CanCheckout: payments.IsEnabled && subscription is not { IsLiveOnStripe: true },
            CanOpenPortal: payments.IsEnabled && subscription?.StripeCustomerId is not null,
            payments.IsEnabled);
    }
}

/// <summary>Commence le paiement d'une formule pour le groupe actif ; renvoie l'adresse de la page Stripe.</summary>
public sealed class StartCheckoutHandler(
    IBillingRepository billing, IPaymentGateway payments, IBandContext band, ICurrentUser currentUser,
    IOptions<AppOptions> app)
{
    public async Task<Uri> HandleAsync(string planId, CancellationToken ct)
    {
        var plan = await billing.GetPlanAsync(planId, ct) ?? throw new NotFoundException("Plan", planId);
        if (plan.IsFree) throw new DomainException("La formule gratuite ne se paie pas : résiliez l'abonnement depuis « Gérer l'abonnement ».");

        var bandId = band.RequiredBandId;
        var subscription = await billing.GetSubscriptionAsync(bandId, ct);
        if (subscription is { IsLiveOnStripe: true })
            throw new ConflictException("Ce groupe a déjà un abonnement : changez de formule depuis « Gérer l'abonnement ».");

        var page = BillingPage(app.Value.PublicUrl, bandId);
        return await payments.CreateCheckoutAsync(new CheckoutRequest(
            bandId, plan.Id, subscription?.StripeCustomerId, currentUser.Email,
            new Uri($"{page}?checkout=success"), new Uri(page)), ct);
    }

    internal static string BillingPage(string publicUrl, Guid bandId) => $"{publicUrl.TrimEnd('/')}/b/{bandId}/billing";
}

/// <summary>Ouvre le portail client Stripe du groupe actif : carte, factures, changement de formule, résiliation.</summary>
public sealed class OpenBillingPortalHandler(
    IBillingRepository billing, IPaymentGateway payments, IBandContext band, IOptions<AppOptions> app)
{
    public async Task<Uri> HandleAsync(CancellationToken ct)
    {
        var bandId = band.RequiredBandId;
        var customerId = (await billing.GetSubscriptionAsync(bandId, ct))?.StripeCustomerId
                         ?? throw new ConflictException("Aucun paiement n'a encore eu lieu pour ce groupe.");
        return await payments.CreatePortalAsync(customerId,
            new Uri(StartCheckoutHandler.BillingPage(app.Value.PublicUrl, bandId)), ct);
    }
}