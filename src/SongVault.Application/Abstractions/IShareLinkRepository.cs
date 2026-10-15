using SongVault.Application.Files;
using SongVault.Application.Sharing;
using SongVault.Domain.Sharing;

namespace SongVault.Application.Abstractions;

/// <summary>
/// Liens de partage et leur journal (ADR 0019). Un seul dépôt pour l'agrégat ShareLink (ADR 0006).
/// Les lectures publiques ignorent le filtre de groupe : c'est le lien qui dit ce qu'on peut lire, et rien d'autre.
/// </summary>
public interface IShareLinkRepository
{
    /// <summary>Ajoute un lien au contexte. N'écrit rien : l'appelant fait SaveChangesAsync.</summary>
    void Add(ShareLink link);

    /// <summary>Retire le lien du contexte ; effacé avec son journal au prochain SaveChangesAsync.</summary>
    void Remove(ShareLink link);

    /// <summary>Ajoute une ligne au journal d'un lien. N'écrit rien : l'appelant fait SaveChangesAsync.</summary>
    void AddAccess(ShareLinkAccess access);

    /// <summary>Lien dont l'empreinte de jeton est celle-ci, quel que soit son groupe, ou null.</summary>
    Task<ShareLink?> GetByTokenHashAsync(byte[] tokenHash, CancellationToken ct);

    /// <summary>Lien suivi par EF, ou null s'il n'existe pas dans ce groupe.</summary>
    Task<ShareLink?> GetInBandAsync(Guid bandId, Guid linkId, CancellationToken ct);

    /// <summary>Vrai si la version (ou la sortie) existe dans ce groupe.</summary>
    Task<bool> TargetExistsAsync(Guid bandId, ShareTargetType type, Guid targetId, CancellationToken ct);

    /// <summary>Liens de cette version ou de cette sortie, les plus récents d'abord, avec leur nombre d'ouvertures.</summary>
    Task<IReadOnlyList<ShareLinkDto>> ListAsync(Guid bandId, ShareTargetType type, Guid targetId, DateTimeOffset now, CancellationToken ct);

    /// <summary>Journal d'un lien, le plus récent d'abord, au plus limit lignes.</summary>
    Task<IReadOnlyList<ShareLinkAccessDto>> ListAccessesAsync(Guid linkId, int limit, CancellationToken ct);

    /// <summary>
    /// Ce que le lien fait écouter, lu au moment de la demande (un fichier ajouté depuis apparaît) :
    /// les fichiers audio de la version, ou ceux de la version choisie pour chaque piste de la sortie.
    /// null si la cible n'existe plus.
    /// </summary>
    Task<SharedContentDto?> ReadContentAsync(ShareLink link, CancellationToken ct);

    /// <summary>Fichier audio de la cible du lien, ou null : un fichier d'un autre morceau n'est jamais trouvé.</summary>
    Task<StoredFileInfo?> GetSharedFileAsync(ShareLink link, Guid fileId, CancellationToken ct);

    /// <summary>Pochette de la sortie du lien, ou null (lien vers une version, ou sortie sans pochette).</summary>
    Task<StoredFileInfo?> GetSharedCoverAsync(ShareLink link, CancellationToken ct);
}