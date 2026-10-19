using SongVault.Application.Accounts;
using SongVault.Domain.Accounts;

namespace SongVault.Application.Abstractions;

/// <summary>Historique de connexion des comptes (#19, ADR 0026).</summary>
public interface IAccountEventRepository
{
    /// <summary>Ajoute la ligne au contexte. N'écrit rien : elle part au prochain SaveChangesAsync.</summary>
    void Add(AccountEvent accountEvent);

    /// <summary>Les take dernières lignes de ce compte, de la plus récente à la plus ancienne. Lecture sans suivi.</summary>
    Task<IReadOnlyList<AccountEventDto>> ListAsync(string userId, int take, CancellationToken ct);
}