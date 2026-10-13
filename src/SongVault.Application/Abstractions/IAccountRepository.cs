using SongVault.Application.Accounts;
using SongVault.Domain.Accounts;

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
}