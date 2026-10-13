using SongVault.Application.Accounts;
using SongVault.Domain.Accounts;
using SongVault.Domain.Bands;

namespace SongVault.Application.Abstractions;

/// <summary>Données du compte de l'utilisateur : exports (et, au commit suivant, suppression).</summary>
public interface IAccountRepository
{
    void AddExport(DataExport export);
    void RemoveExport(DataExport export);
    Task<DataExport?> GetExportAsync(Guid exportId, CancellationToken ct);
    /// <summary>Dernier export demandé par ce compte, quel que soit son état.</summary>
    Task<DataExport?> GetLatestExportAsync(string userId, CancellationToken ct);
    /// <summary>Tout ce que le compte peut consulter ; null si le compte n'existe plus.</summary>
    Task<AccountData?> ReadAccountDataAsync(string userId, DateTimeOffset now, CancellationToken ct);

    // ---- Suppression du compte ----
    Task<bool> CheckPasswordAsync(string userId, string password, CancellationToken ct);
    /// <summary>Groupes du compte, suivis, avec toutes leurs adhésions.</summary>
    Task<IReadOnlyList<Band>> ListBandsAsync(string userId, CancellationToken ct);
    /// <summary>Verrouille le compte sans fin et coupe ses sessions. Enregistré par le prochain SaveChangesAsync.</summary>
    Task DeactivateAsync(string userId, CancellationToken ct);
    /// <summary>
    /// Efface le compte et ce qui n'appartient qu'à lui, en une transaction ; renvoie les clés des fichiers à effacer.
    /// Sans effet (liste vide) si le compte n'existe plus ou a été réactivé.
    /// </summary>
    Task<IReadOnlyList<string>> PurgeAsync(string userId, CancellationToken ct);
}