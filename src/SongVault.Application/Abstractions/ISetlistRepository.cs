using SongVault.Application.Setlists;
using SongVault.Domain.Setlists;

namespace SongVault.Application.Abstractions;

/// <summary>
/// Setlists du groupe actif. Un seul dépôt pour l'agrégat Setlist et ses lignes (ADR 0006).
/// Une setlist d'un autre groupe est « introuvable ».
/// </summary>
public interface ISetlistRepository
{
    /// <summary>Ajoute une setlist au contexte. N'écrit rien : l'appelant fait SaveChangesAsync.</summary>
    void Add(Setlist setlist);

    /// <summary>Retire la setlist du contexte ; effacée avec ses lignes au prochain SaveChangesAsync.</summary>
    void Remove(Setlist setlist);

    /// <summary>Setlist suivie par EF, avec ses lignes, si elle est dans le groupe actif ; sinon null.</summary>
    Task<Setlist?> GetAsync(Guid setlistId, CancellationToken ct);

    /// <summary>Setlists du groupe : sans date d'abord (de travail), puis de la plus récente à la plus ancienne.</summary>
    Task<IReadOnlyList<SetlistSummaryDto>> ListAsync(CancellationToken ct);

    /// <summary>
    /// Setlist et lignes dans l'ordre : titre du morceau, versions, tonalité et BPM de la version jouée,
    /// durée de son fichier audio (#15). Lecture sans suivi ; le total se calcule dans SetlistDto.
    /// </summary>
    Task<SetlistDto?> GetDetailAsync(Guid setlistId, CancellationToken ct);

    /// <summary>Noms des setlists du groupe où figure ce morceau (refus de sa mise à la corbeille).</summary>
    Task<IReadOnlyList<string>> ListNamesWithSongAsync(Guid songId, CancellationToken ct);

    /// <summary>Noms des setlists du groupe dont une ligne joue explicitement cette version (refus de sa mise à la corbeille).</summary>
    Task<IReadOnlyList<string>> ListNamesWithVersionAsync(Guid versionId, CancellationToken ct);
}