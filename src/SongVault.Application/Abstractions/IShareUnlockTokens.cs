namespace SongVault.Application.Abstractions;

/// <summary>
/// Preuve, posée en cookie, que ce navigateur a donné le mot de passe d'un lien (ADR 0019) :
/// signée, liée à un seul lien, avec sa propre date de fin.
/// </summary>
public interface IShareUnlockTokens
{
    string Create(Guid linkId, DateTimeOffset expiresAt);

    /// <summary>false si le jeton a été modifié ou ne vient pas de SongVault.</summary>
    bool TryRead(string token, out Guid linkId, out DateTimeOffset expiresAt);
}