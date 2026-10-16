using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Options;

using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;

namespace SongVault.Infrastructure.Billing;

/// <summary>
/// Vérifie la signature d'un appel du webhook Stripe, puis lit l'événement (ADR 0023).
/// En-tête Stripe-Signature : « t=horodatage,v1=signature[,v1=…] » ; la signature est un HMAC-SHA256
/// de « horodatage.corps » avec le secret du webhook, en hexadécimal. Un horodatage trop vieux est refusé.
/// </summary>
internal sealed class StripeEventReader(IOptions<BillingOptions> options, TimeProvider clock) : IPaymentEventReader
{
    public PaymentEvent Read(string payload, string? signatureHeader)
    {
        var stripe = options.Value.Stripe;
        if (string.IsNullOrWhiteSpace(stripe.WebhookSecret))
            throw new BillingUnavailableException("Le webhook Stripe n'est pas configuré.");
        Verify(payload, signatureHeader, stripe.WebhookSecret, stripe.SignatureTolerance);

        using var json = JsonDocument.Parse(payload);
        var root = json.RootElement;
        var type = root.GetProperty("type").GetString()!;
        var data = root.GetProperty("data").GetProperty("object");

        return new PaymentEvent(root.GetProperty("id").GetString()!, type, SubscriptionId(type, data), BandId(data));
    }

    private void Verify(string payload, string? header, string secret, TimeSpan tolerance)
    {
        if (string.IsNullOrWhiteSpace(header)) throw new InvalidPaymentEventException("Signature Stripe absente.");

        var parts = header.Split(',', StringSplitOptions.TrimEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToList();
        var timestamp = parts.FirstOrDefault(p => p[0] == "t")?[1];
        if (!long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
            throw new InvalidPaymentEventException("Signature Stripe sans horodatage.");
        if (clock.GetUtcNow() - DateTimeOffset.FromUnixTimeSeconds(seconds) > tolerance)
            throw new InvalidPaymentEventException("Signature Stripe trop ancienne.");

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        var valid = parts.Where(p => p[0] == "v1").Any(p =>
            p[1].Length == expected.Length * 2 && p[1].All(Uri.IsHexDigit)
            && CryptographicOperations.FixedTimeEquals(Convert.FromHexString(p[1]), expected));
        if (!valid) throw new InvalidPaymentEventException("Signature Stripe invalide.");
    }

    // L'abonnement concerné, selon le type d'objet ; null pour ce qui ne touche aucun abonnement
    private static string? SubscriptionId(string type, JsonElement data)
    {
        if (type.StartsWith("customer.subscription.", StringComparison.Ordinal)) return String(data, "id");
        // Facture : « subscription » jusqu'à la version 2025-03-31 de l'API, puis sous « parent »
        return String(data, "subscription")
               ?? (data.TryGetProperty("parent", out var parent) && parent.ValueKind == JsonValueKind.Object
                   && parent.TryGetProperty("subscription_details", out var details) && details.ValueKind == JsonValueKind.Object
                       ? String(details, "subscription")
                       : null);
    }

    // client_reference_id sur une session Checkout, metadata.band_id sur un abonnement
    private static Guid? BandId(JsonElement data)
    {
        var value = String(data, "client_reference_id")
                    ?? (data.TryGetProperty("metadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object
                        ? String(metadata, "band_id")
                        : null);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private static string? String(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}