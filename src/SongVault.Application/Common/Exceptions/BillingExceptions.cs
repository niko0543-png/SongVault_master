namespace SongVault.Application.Common.Exceptions;

/// <summary>Limite de la formule du groupe atteinte (stockage plein, groupe gratuit en trop). Traduite en HTTP 402.</summary>
public sealed class PlanLimitException(string message) : Exception(message);

/// <summary>Paiement en ligne pas ouvert, ou Stripe injoignable. Traduite en HTTP 503.</summary>
public sealed class BillingUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Appel du webhook sans signature Stripe valable. Traduite en HTTP 400.</summary>
public sealed class InvalidPaymentEventException(string message) : Exception(message);