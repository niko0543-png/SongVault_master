using SongVault.Application.Activity;
using SongVault.Application.Audit;
using SongVault.Domain.Audit;

namespace SongVault.Application.Abstractions;

/// <summary>Journal d'audit des groupes (#19, ADR 0026). Les lignes ne se modifient ni ne se suppriment.</summary>
public interface IAuditRepository
{
    /// <summary>Ajoute la ligne au contexte. N'écrit rien : elle part au SaveChangesAsync de l'action.</summary>
    void Add(AuditEntry entry);

    /// <summary>
    /// Au plus take lignes du groupe actif, de la plus récente à la plus ancienne, strictement après le curseur
    /// (null : depuis la plus récente). Avec l'e-mail de l'auteur et celui du compte visé. Lecture sans suivi.
    /// </summary>
    Task<IReadOnlyList<AuditItemDto>> ListAsync(ActivityCursor? after, int take, CancellationToken ct);
}