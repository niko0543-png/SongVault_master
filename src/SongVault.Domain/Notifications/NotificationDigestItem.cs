using SongVault.Domain.Common;

namespace SongVault.Domain.Notifications;

/// <summary>Un événement qui attend le résumé quotidien d'un compte (ADR 0018). Effacé une fois le résumé parti.</summary>
public sealed class NotificationDigestItem
{
    private NotificationDigestItem() { } // réservé à EF Core

    public long Id { get; private set; }
    public string UserId { get; private set; } = default!;
    /// <summary>L'événement du fil d'activité (ActivityEvents.Id) à raconter.</summary>
    public long EventId { get; private set; }
    public NotificationKind Kind { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static NotificationDigestItem Create(string userId, long eventId, NotificationKind kind, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new DomainException("Un résumé appartient à un compte.");
        if (eventId <= 0) throw new DomainException("Un élément de résumé raconte un événement.");
        if (!Enum.IsDefined(kind)) throw new DomainException($"Type de notification inconnu : {kind}.");
        return new NotificationDigestItem { UserId = userId, EventId = eventId, Kind = kind, CreatedAt = now };
    }
}