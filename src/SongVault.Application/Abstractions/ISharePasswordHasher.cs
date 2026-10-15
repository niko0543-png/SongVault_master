namespace SongVault.Application.Abstractions;

/// <summary>Hachage du mot de passe facultatif d'un lien de partage (même algorithme que les comptes, ADR 0019).</summary>
public interface ISharePasswordHasher
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);
}