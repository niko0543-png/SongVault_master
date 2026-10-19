namespace SongVault.Domain.Trash;

/// <summary>
/// Supprimé en deux temps (#20, ADR 0027) : d'abord à la corbeille (DeletedAt renseigné), restaurable pendant
/// <see cref="TrashPolicy.Retention"/>, puis effacé pour de bon par la purge. Morceaux, versions et fichiers.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>Mise à la corbeille ; null tant que l'élément est en place.</summary>
    DateTimeOffset? DeletedAt { get; }
    /// <summary>Compte qui l'a mis à la corbeille.</summary>
    string? DeletedById { get; }
}

public static class TrashPolicy
{
    /// <summary>Durée pendant laquelle un élément de la corbeille se restaure, avant la purge.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    /// <summary>Date à laquelle la purge effacera l'élément.</summary>
    public static DateTimeOffset PurgeAt(DateTimeOffset deletedAt) => deletedAt + Retention;
}

/// <summary>Ce qu'on trouve dans la corbeille : un morceau entier, une version seule, ou un fichier seul.</summary>
public enum TrashItemKind
{
    Song,
    Version,
    File,
}