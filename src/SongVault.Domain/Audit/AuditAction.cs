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
}