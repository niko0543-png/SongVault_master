using SongVault.Application.Bands;
using SongVault.Domain.Bands;

namespace SongVault.Application.Abstractions;

/// <summary>Accès aux groupes et à leurs adhésions. Un seul dépôt pour l'agrégat Band (ADR 0006).</summary>
public interface IBandRepository
{
    /// <summary>
    /// Ajoute un groupe neuf au contexte, avec l'adhésion Owner créée par Band.Create.
    /// N'écrit rien en base : l'appelant fait IUnitOfWork.SaveChangesAsync.
    /// Utilisé par CreateBandHandler et ListMyBandsHandler (groupe personnel).
    /// </summary>
    void Add(Band band);

    /// <summary>
    /// Groupe suivi par EF (pour le modifier), ou null s'il n'existe pas.
    /// Utilisé par RenameBandHandler.
    /// </summary>
    Task<Band?> GetByIdAsync(Guid bandId, CancellationToken ct);

    /// <summary>
    /// Rôle de l'utilisateur dans le groupe, ou null s'il n'en est pas membre (ou si le groupe n'existe pas).
    /// Lecture sans suivi. Utilisé par BandScopedFilter : null donne une 404.
    /// </summary>
    Task<BandRole?> GetRoleAsync(Guid bandId, string userId, CancellationToken ct);

    /// <summary>
    /// Groupes dont l'utilisateur est membre, triés par nom, avec son rôle dans chacun.
    /// Liste vide si aucun. Lecture sans suivi. Utilisé par GET /api/bands (ListMyBandsHandler).
    /// </summary>
    Task<IReadOnlyList<BandSummaryDto>> ListForUserAsync(string userId, CancellationToken ct);
}