using SongVault.Application.Common;
using SongVault.Application.Files;
using SongVault.Application.Files.Stems;
using SongVault.Application.Songs;
using SongVault.Application.Versions;
using SongVault.Domain.Songs;
using SongVault.Domain.Files;

namespace SongVault.Application.Abstractions;

public interface ISongRepository
{
    void Add(Song song);
    Task<Song?> GetByIdAsync(Guid id, CancellationToken ct);
    /// <param name="released">#38 : true = morceaux sortis, false = en travail, null = tous.</param>
    Task<PagedResult<SongDto>> ListAsync(int page, int pageSize, string? search, SongVersionStatus? status, bool? released, CancellationToken ct);
    /// <summary>Les morceaux du groupe actif parus sur cette sortie (#38), suivis par EF.</summary>
    Task<IReadOnlyList<Song>> ListReleasedOnAsync(Guid releaseId, CancellationToken ct);
    Task<Song?> GetWithVersionAsync(Guid songId, Guid versionId, CancellationToken ct);
    /// <summary>Le morceau avec toutes ses versions et leurs fichiers en place, pour le mettre à la corbeille (#20).</summary>
    Task<Song?> GetWithAllVersionsAsync(Guid songId, CancellationToken ct);
    Task<bool> ExistsAsync(Guid songId, CancellationToken ct);
    Task<IReadOnlyList<SongVersionSummaryDto>> ListVersionsAsync(Guid songId, CancellationToken ct);
    Task<SongVersionDto?> GetVersionAsync(Guid songId, Guid versionId, CancellationToken ct);
    Task<StoredFileInfo?> GetFileAsync(Guid songId, Guid versionId, Guid fileId, CancellationToken ct);
    /// <summary>Le fichier, son morceau et sa version, s'il est dans cette version de ce morceau du groupe actif ; sinon null (#8, #11).</summary>
    Task<SongFileLabel?> GetFileLabelAsync(Guid songId, Guid versionId, Guid fileId, CancellationToken ct);
    /// <summary>Le fichier à écouter : le MP3 d'aperçu s'il est prêt, sinon l'original (ADR 0021).</summary>
    Task<StoredFileInfo?> GetListenFileAsync(Guid songId, Guid versionId, Guid fileId, CancellationToken ct);
    /// <summary>Forme d'onde d'un fichier de ce groupe, si son aperçu est prêt (ADR 0021).</summary>
    Task<AudioWaveformDto?> GetWaveformAsync(Guid songId, Guid versionId, Guid fileId, CancellationToken ct);
    /// <summary>Pour chaque version trouvée dans le groupe actif, l'identifiant de son morceau (#10). Les autres sont absentes.</summary>
    Task<IReadOnlyDictionary<Guid, Guid>> GetVersionSongIdsAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken ct);
    /// <summary>Ceux de ces morceaux qui sont dans le groupe actif, hors corbeille (#21).</summary>
    Task<IReadOnlySet<Guid>> ListExistingIdsAsync(IReadOnlyCollection<Guid> songIds, CancellationToken ct);
    /// <summary>Le morceau suivi par EF avec ses crédits et ses versions en place (#22), s'il est dans le groupe actif.</summary>
    Task<Song?> GetWithRightsAsync(Guid songId, CancellationToken ct);
    /// <summary>Ceux de ces morceaux qui sont dans le groupe actif, avec crédits et versions, en lecture seule (#22).</summary>
    Task<IReadOnlyList<Song>> ListWithRightsAsync(IReadOnlyCollection<Guid> songIds, CancellationToken ct);
    /// <summary>Les pistes du mélangeur de cette version (#23), dans l'ordre ; null si la version n'est pas dans le groupe actif.</summary>
    Task<IReadOnlyList<StemDto>?> ListStemsAsync(Guid songId, Guid versionId, CancellationToken ct);
}