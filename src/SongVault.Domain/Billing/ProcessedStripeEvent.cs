using SongVault.Domain.Common;

namespace SongVault.Domain.Billing;

/// <summary>
/// Un événement Stripe déjà traité (ADR 0023). Stripe renvoie un événement tant qu'il n'a pas reçu 2xx, parfois
/// deux fois : la clé primaire sur EventId fait qu'un même événement ne compte qu'une fois.
/// </summary>
public sealed class ProcessedStripeEvent
{
    public const int EventIdMaxLength = 255;
    public const int TypeMaxLength = 100;

    private ProcessedStripeEvent() { } // réservé à EF Core

    public string EventId { get; private set; } = default!;
    public string Type { get; private set; } = default!;
    public DateTimeOffset ProcessedAt { get; private set; }

    public static ProcessedStripeEvent Create(string eventId, string type, DateTimeOffset now)
        => new()
        {
            EventId = Text.Required(eventId, EventIdMaxLength, "identifiant d'événement"),
            Type = Text.Required(type, TypeMaxLength, "type d'événement"),
            ProcessedAt = now,
        };
}