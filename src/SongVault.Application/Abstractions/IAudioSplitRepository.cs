using SongVault.Application.Files.Splits;
using SongVault.Domain.Files;
using SongVault.Domain.Songs;

namespace SongVault.Application.Abstractions;

/// <summary>
/// Découpages d'enregistrements (ADR 0050). Le groupe est toujours passé en paramètre : la tâche de fond n'a pas
/// de groupe actif, et les gestionnaires HTTP passent celui que [BandScoped] a vérifié.
/// </summary>
public interface IAudioSplitRepository
{
    void Add(AudioSplit split);

    Task<AudioSplit?> GetAsync(Guid splitId, CancellationToken ct);

    /// <summary>Dernier découpage demandé pour ce fichier du groupe, ou null.</summary>
    Task<AudioSplit?> GetLatestAsync(Guid bandId, Guid sourceFileId, CancellationToken ct);

    /// <summary>Le fichier en place de cette version de ce morceau du groupe, avec son analyse et son aperçu ; sinon null.</summary>
    Task<AudioSplitSource?> GetSourceAsync(Guid bandId, Guid songId, Guid versionId, Guid fileId, CancellationToken ct);

    /// <summary>Ceux de ces morceaux qui sont dans le groupe, hors corbeille.</summary>
    Task<IReadOnlySet<Guid>> ListLiveSongIdsAsync(Guid bandId, IReadOnlyCollection<Guid> songIds, CancellationToken ct);

    /// <summary>Le morceau du groupe, hors corbeille, suivi par EF pour lui ajouter une version ; sinon null.</summary>
    Task<Song?> GetSongAsync(Guid bandId, Guid songId, CancellationToken ct);

    void AddSong(Song song);
}
