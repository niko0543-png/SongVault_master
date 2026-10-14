using SongVault.Domain.Notifications;

namespace SongVault.Application.Abstractions;

/// <summary>
/// Jetons des liens de désinscription (ADR 0018) : signés, utilisables sans connexion, sans date d'expiration.
/// Kind null : tous les types (lien du résumé quotidien).
/// </summary>
public interface IUnsubscribeTokens
{
    string Create(string userId, NotificationKind? kind);
    /// <summary>false si le jeton a été modifié ou ne vient pas de SongVault.</summary>
    bool TryRead(string token, out string userId, out NotificationKind? kind);
}