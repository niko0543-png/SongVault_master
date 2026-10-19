/** Même liste que TrashItemKind côté C# (#20). */
export type TrashItemKind = 'Song' | 'Version' | 'File'

/** Une ligne de la corbeille (GET /api/bands/{id}/trash, Member et Owner). */
export interface TrashItem {
  kind: TrashItemKind
  id: string
  songId: string
  songTitle: string
  /** Pour une version ou un fichier */
  versionNumber: number | null
  versionTitle: string | null
  /** Pour un fichier */
  fileName: string | null
  /** Place encore occupée, jusqu'à la purge */
  sizeBytes: number
  deletedAt: string
  deletedById: string | null
  /** null avec un deletedById : compte supprimé */
  deletedByEmail: string | null
  /** Date de la suppression définitive */
  purgeAt: string
}