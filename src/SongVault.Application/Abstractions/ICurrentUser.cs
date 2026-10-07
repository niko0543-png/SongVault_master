namespace SongVault.Application.Abstractions;

/// <summary>Utilisateur à l'origine de la requête en cours.</summary>
public interface ICurrentUser
{
    string UserId { get; }
}