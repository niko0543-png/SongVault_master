/** Même liste que AuditAction côté C# (#19). */
export type AuditAction =
  | 'SongDeleted' | 'FileDeleted' | 'CommentDeleted' | 'ReleaseDeleted'
  | 'ShareLinkCreated' | 'ShareLinkRevoked' | 'ShareLinkDeleted'
  | 'InvitationCreated' | 'InvitationRevoked' | 'InvitationAccepted'
  | 'MemberRoleChanged' | 'MemberRemoved' | 'MemberLeft'
  | 'BandRenamed' | 'FileQuarantined'
  | 'VersionDeleted' | 'SongRestored' | 'VersionRestored' | 'FileRestored'
    | 'SongPurged' | 'VersionPurged' | 'FilePurged'
  | 'CreditsChanged'

/** Une ligne du journal d'audit (GET /api/bands/{id}/audit, Owner seulement). */
export interface AuditItem {
  id: number
  action: AuditAction
  /** null : SongVault lui-même (antivirus) */
  actorId: string | null
  /** null avec un actorId : compte supprimé */
  actorEmail: string | null
  /** Ce qui est visé, tel qu'il s'appelait */
  subject: string | null
  /** Compte visé : membre retiré, rôle changé, auteur d'un commentaire supprimé */
  targetUserId: string | null
  targetEmail: string | null
  details: string | null
  occurredAt: string
}

export interface AuditPage {
  items: AuditItem[]
  /** null : plus rien de plus ancien */
  nextCursor: string | null
}