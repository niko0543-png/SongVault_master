using SongVault.Domain.Billing;

namespace SongVault.Application.Abstractions;

/// <summary>Paiement d'une formule : le groupe, la formule, le client Stripe s'il existe, et où revenir.</summary>
public sealed record CheckoutRequest(
    Guid BandId, string PlanId, string? CustomerId, string? CustomerEmail, Uri SuccessUrl, Uri CancelUrl);

/// <summary>
/// Le prestataire de paiement (ADR 0023). Stripe en production, rien en développement tant que
/// Billing:Provider vaut None : chaque appel lève alors BillingUnavailableException.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Faux : le paiement en ligne n'est pas ouvert (le front l'annonce au lieu d'un bouton).</summary>
    bool IsEnabled { get; }
    /// <summary>Page de paiement Stripe Checkout, en mode abonnement.</summary>
    Task<Uri> CreateCheckoutAsync(CheckoutRequest request, CancellationToken ct);
    /// <summary>Portail client Stripe : carte, factures, changement de formule, résiliation.</summary>
    Task<Uri> CreatePortalAsync(string customerId, Uri returnUrl, CancellationToken ct);
    /// <summary>État actuel de l'abonnement chez Stripe ; null s'il n'existe pas.</summary>
    Task<SubscriptionState?> GetSubscriptionAsync(string subscriptionId, CancellationToken ct);
    /// <summary>Arrête l'abonnement tout de suite, sans remboursement. Sans effet s'il est déjà arrêté.</summary>
    Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct);
}

/// <summary>Un événement reçu de Stripe, réduit à ce qui sert : l'abonnement concerné et le groupe s'il est donné.</summary>
public sealed record PaymentEvent(string Id, string Type, string? SubscriptionId, Guid? BandId);

/// <summary>Lit un événement envoyé par Stripe à la route du webhook, après en avoir vérifié la signature.</summary>
public interface IPaymentEventReader
{
    /// <summary>Lève InvalidPaymentEventException si la signature manque, est fausse ou trop ancienne.</summary>
    PaymentEvent Read(string payload, string? signatureHeader);
}