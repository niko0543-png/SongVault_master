namespace SongVault.Domain.Audit;

/// <summary>
/// Ce qu'un membre (ou SongVault lui-même) a fait dans le groupe (#19, ADR 0026). Enregistré en texte dans
/// AuditEntries.Action : renommer une valeur demande de migrer les lignes existantes ; en ajouter une ne demande rien.
/// </summary>
public enum AuditAction
{
    SongDeleted,
    FileDeleted,
    CommentDeleted,
    ReleaseDeleted,
    ShareLinkCreated,
    ShareLinkRevoked,
    ShareLinkDeleted,
    InvitationCreated,
    InvitationRevoked,
    InvitationAccepted,
    MemberRoleChanged,
    MemberRemoved,
    MemberLeft,
    BandRenamed,
    /// <summary>Fichier bloqué par l'antivirus (#17) : écrit par SongVault, sans auteur.</summary>
    FileQuarantined,
    // #20 : SongDeleted et FileDeleted veulent dire « mis à la corbeille » ; la suite, restauration et purge
    VersionDeleted,
    SongRestored,
    VersionRestored,
    FileRestored,
    /// <summary>Supprimé définitivement : par un Owner depuis la corbeille, ou par la purge à J+30 (sans auteur).</summary>
    SongPurged,
    VersionPurged,
    FilePurged,
    /// <summary>Crédits ou ISWC d'un morceau enregistrés (#22) : qui touche aux parts des droits doit se voir.</summary>
    CreditsChanged,
}