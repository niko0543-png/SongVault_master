namespace SongVault.Application.Abstractions;

/// <summary>Dernière ouverture de chaque morceau par chaque membre (#11).</summary>
public interface ISongViewRepository
{
    /// <summary>
    /// Note que l'utilisateur vient d'ouvrir le morceau. Écrit tout de suite, sans attendre un SaveChangesAsync :
    /// il n'y a rien d'autre à enregistrer avec, et deux onglets qui l'appellent ensemble ne sont pas une erreur.
    /// </summary>
    Task MarkSeenAsync(Guid songId, string userId, DateTimeOffset now, CancellationToken ct);
}