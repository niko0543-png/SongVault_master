using SongVault.Application.Notifications;
using SongVault.Domain.Notifications;

namespace SongVault.Application.Abstractions;

/// <summary>
/// Notifications par e-mail (ADR 0018). Utilisé hors requête HTTP, par les tâches de fond :
/// aucune lecture n'est limitée au groupe actif, chaque méthode dit ce qu'elle lit.
/// </summary>
public interface INotificationRepository
{
    // ---- Préférences ----

    /// <summary>Préférences enregistrées du compte, suivies (pour les modifier). Un type absent : mode par défaut.</summary>
    Task<IReadOnlyList<NotificationPreference>> ListPreferencesAsync(string userId, CancellationToken ct);
    void AddPreference(NotificationPreference preference);
    /// <summary>Modes enregistrés par ces comptes. Un couple absent du dictionnaire : mode par défaut du type.</summary>
    Task<IReadOnlyDictionary<(string UserId, NotificationKind Kind), NotificationMode>> GetModesAsync(
        IReadOnlyCollection<string> userIds, CancellationToken ct);

    // ---- Préparation ----

    /// <summary>
    /// Prend au plus take événements pas encore traités, les plus anciens d'abord, et pose leur NotifiedAt.
    /// Deux instances de l'API ne prennent jamais le même ; un événement pas encore validé est laissé pour plus tard.
    /// À appeler dans une transaction : si la suite échoue, les événements redeviennent à traiter.
    /// </summary>
    Task<IReadOnlyList<long>> ClaimEventsAsync(int take, DateTimeOffset now, CancellationToken ct);
    /// <summary>Ces événements, avec le nom du groupe, l'e-mail de l'auteur et le texte du commentaire.</summary>
    Task<IReadOnlyList<NotificationEvent>> GetEventsAsync(IReadOnlyCollection<long> eventIds, CancellationToken ct);
    /// <summary>Membres du groupe (tous rôles) avec leur adresse.</summary>
    Task<IReadOnlyList<NotificationRecipient>> ListMembersAsync(Guid bandId, CancellationToken ct);
    void AddDigestItem(NotificationDigestItem item);

    // ---- Envoi ----

    /// <summary>Le compte s'il est encore membre du groupe ; sinon null.</summary>
    Task<NotificationRecipient?> GetMemberAsync(Guid bandId, string userId, CancellationToken ct);
    /// <summary>Comptes qui ont des éléments en attente de résumé.</summary>
    Task<IReadOnlyList<string>> ListDigestUsersAsync(CancellationToken ct);
    /// <summary>Éléments en attente de résumé du compte, du plus ancien au plus récent, avec leur événement.</summary>
    Task<IReadOnlyList<DigestEntry>> ListDigestAsync(string userId, CancellationToken ct);
    /// <summary>Adresse du compte ; null s'il n'existe plus.</summary>
    Task<string?> GetEmailAsync(string userId, CancellationToken ct);
    /// <summary>Efface ces éléments de résumé, tout de suite (pas besoin de SaveChangesAsync).</summary>
    Task DeleteDigestItemsAsync(IReadOnlyCollection<long> itemIds, CancellationToken ct);
}