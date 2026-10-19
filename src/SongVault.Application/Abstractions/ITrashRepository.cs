using SongVault.Application.Trash;
using SongVault.Domain.Songs;
using SongVault.Domain.Trash;

namespace SongVault.Application.Abstractions;

/// <summary>Corbeille des groupes (#20, ADR 0027) : ce qui a été supprimé, en attendant la restauration ou la purge.</summary>
public interface ITrashRepository
{
    /// <summary>Corbeille du groupe actif, la plus récente d'abord. Une version d'un morceau à la corbeille n'y figure pas seule.</summary>
    Task<IReadOnlyList<TrashItemDto>> ListAsync(CancellationToken ct);

    /// <summary>
    /// Le morceau du groupe actif qui contient cet élément, chargé avec toutes ses versions et tous leurs fichiers,
    /// corbeille comprise ; null si l'élément n'existe pas dans ce groupe.
    /// </summary>
    Task<Song?> GetSongAsync(TrashItemKind kind, Guid itemId, CancellationToken ct);

    /// <summary>Tous groupes confondus : ce qui est à la corbeille depuis cette date ou avant, morceaux d'abord.</summary>
    Task<IReadOnlyList<TrashEntry>> ListExpiredAsync(DateTimeOffset deletedUpTo, CancellationToken ct);

    /// <summary>Tous groupes confondus : ce qu'il faut effacer pour cet élément, s'il est à la corbeille ; sinon null.</summary>
    Task<PurgeTarget?> GetPurgeTargetAsync(TrashItemKind kind, Guid itemId, CancellationToken ct);

    /// <summary>Efface l'élément de la base, tout de suite ; versions, fichiers, commentaires et aperçus partent en cascade.</summary>
    Task DeleteAsync(TrashItemKind kind, Guid itemId, CancellationToken ct);
}