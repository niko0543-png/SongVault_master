using SongVault.Domain.Common;

namespace SongVault.Domain.Notifications;

/// <summary>
/// Choix d'un compte pour un type d'e-mail, valable dans tous ses groupes (ADR 0018).
/// Pas de ligne : c'est le mode par défaut du type qui s'applique.
/// </summary>
public sealed class NotificationPreference
{
    private NotificationPreference() { } // réservé à EF Core

    public string UserId { get; private set; } = default!;
    public NotificationKind Kind { get; private set; }
    public NotificationMode Mode { get; private set; }

    /// <summary>
    /// Mode d'un compte qui n'a rien choisi. Les commentaires vont au résumé quotidien : sur un morceau
    /// en cours de travail, il y en a vite plusieurs par jour.
    /// </summary>
    public static NotificationMode DefaultMode(NotificationKind kind) => kind switch
    {
        NotificationKind.NewVersion => NotificationMode.Immediate,
        NotificationKind.Comment => NotificationMode.Daily,
        NotificationKind.Mention => NotificationMode.Immediate,
        _ => throw new DomainException($"Type de notification inconnu : {kind}."),
    };

    public static NotificationPreference Create(string userId, NotificationKind kind, NotificationMode mode)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new DomainException("Une préférence appartient à un compte.");
        if (!Enum.IsDefined(kind)) throw new DomainException($"Type de notification inconnu : {kind}.");
        var preference = new NotificationPreference { UserId = userId, Kind = kind };
        preference.ChangeTo(mode);
        return preference;
    }

    public void ChangeTo(NotificationMode mode)
    {
        if (!Enum.IsDefined(mode)) throw new DomainException($"Mode de notification inconnu : {mode}.");
        Mode = mode;
    }
}