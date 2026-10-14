using SongVault.Application.Releases;
using SongVault.Domain.Releases;

namespace SongVault.Application.Abstractions;

/// <summary>
/// Sorties du groupe actif. Un seul dépôt pour l'agrégat Release et ses pistes (ADR 0006).
/// Une sortie d'un autre groupe est « introuvable ».
/// </summary>
public interface IReleaseRepository
{
    /// <summary>Ajoute une sortie au contexte. N'écrit rien : l'appelant fait SaveChangesAsync.</summary>
    void Add(Release release);

    /// <summary>Retire la sortie du contexte ; effacée avec ses pistes au prochain SaveChangesAsync.</summary>
    void Remove(Release release);

    /// <summary>Sortie suivie par EF, avec ses pistes, si elle est dans le groupe actif ; sinon null.</summary>
    Task<Release?> GetAsync(Guid releaseId, CancellationToken ct);

    /// <summary>Sorties du groupe : sans date d'abord (en préparation), puis de la plus récente à la plus ancienne.</summary>
    Task<IReadOnlyList<ReleaseSummaryDto>> ListAsync(CancellationToken ct);

    /// <summary>Sortie et pistes dans l'ordre, avec le titre du morceau et ses versions. Lecture sans suivi.</summary>
    Task<ReleaseDto?> GetDetailAsync(Guid releaseId, CancellationToken ct);

    /// <summary>Titres des sorties du groupe où figure ce morceau (refus de suppression du morceau).</summary>
    Task<IReadOnlyList<string>> ListTitlesWithSongAsync(Guid songId, CancellationToken ct);
}