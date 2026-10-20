using SongVault.Application.Releases.Exports;
using SongVault.Domain.Releases;

namespace SongVault.Application.Abstractions;

/// <summary>
/// Archives des sorties (#24, ADR 0031). Le groupe est toujours passé en paramètre : la tâche de fond n'a pas
/// de groupe actif, et les gestionnaires HTTP passent celui que [BandScoped] a vérifié.
/// </summary>
public interface IReleaseExportRepository
{
    void Add(ReleaseExport export);

    void Remove(ReleaseExport export);

    Task<ReleaseExport?> GetAsync(Guid exportId, CancellationToken ct);

    /// <summary>Dernière archive demandée pour cette sortie, ou null.</summary>
    Task<ReleaseExport?> GetLatestAsync(Guid bandId, Guid releaseId, CancellationToken ct);

    /// <summary>Titre de la sortie, ou null si elle n'est pas dans ce groupe.</summary>
    Task<string?> GetReleaseTitleAsync(Guid bandId, Guid releaseId, CancellationToken ct);

    /// <summary>
    /// Tout ce qui entre dans l'archive : la sortie et ses pistes, les morceaux avec leurs crédits et leurs versions,
    /// les fichiers audio des versions choisies avec leur analyse antivirus. null si la sortie n'est pas dans ce groupe.
    /// </summary>
    Task<ReleaseArchiveSource?> ReadSourceAsync(Guid bandId, Guid releaseId, CancellationToken ct);
}