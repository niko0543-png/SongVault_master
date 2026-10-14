using SongVault.Application.Activity;
using SongVault.Domain.Activity;

namespace SongVault.Application.Abstractions;

/// <summary>Fil d'activité du groupe actif (ADR 0017). Les événements ne se modifient ni ne se suppriment.</summary>
public interface IActivityRepository
{
    /// <summary>Ajoute l'événement au contexte. N'écrit rien : il part au SaveChangesAsync de la modification.</summary>
    void Add(ActivityEvent activityEvent);

    /// <summary>
    /// Au plus take événements du groupe actif, du plus récent au plus ancien, strictement après le curseur
    /// (null : depuis le plus récent). Avec l'e-mail de l'auteur. Lecture sans suivi.
    /// </summary>
    Task<IReadOnlyList<ActivityItemDto>> ListAsync(ActivityCursor? after, int take, CancellationToken ct);
}