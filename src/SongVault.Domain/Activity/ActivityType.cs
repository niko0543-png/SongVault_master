namespace SongVault.Domain.Activity;

/// <summary>
/// Ce qui s'est passé sur un morceau (#11). Enregistré en texte dans ActivityEvents.Type :
/// renommer une valeur demande de migrer les lignes existantes ; en ajouter une ne demande rien.
/// </summary>
public enum ActivityType
{
    SongCreated,
    SongUpdated,
    SongDeleted,
    VersionAdded,
    VersionUpdated,
    FileAdded,
    FileRemoved,
    CommentAdded,
    CommentReplied,
}