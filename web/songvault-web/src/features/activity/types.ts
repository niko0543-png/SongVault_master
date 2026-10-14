/** Même liste que ActivityType côté C# (#11). */
export type ActivityType =
  | 'SongCreated' | 'SongUpdated' | 'SongDeleted'
  | 'VersionAdded' | 'VersionUpdated'
  | 'FileAdded' | 'FileRemoved'
  | 'CommentAdded' | 'CommentReplied'

export interface ActivityItem {
  id: number
  type: ActivityType
  /** null : compte supprimé */
  actorId: string | null
  actorEmail: string | null
  songId: string
  /** false : le morceau a été supprimé depuis ; songTitle garde son titre d'alors */
  songExists: boolean
  versionId: string | null
  /** Fichier ou commentaire concerné */
  entityId: string | null
  songTitle: string
  versionNumber: number | null
  versionTitle: string | null
  fileName: string | null
  createdAt: string
}

export interface ActivityPage {
  items: ActivityItem[]
  /** null : plus rien de plus ancien */
  nextCursor: string | null
}