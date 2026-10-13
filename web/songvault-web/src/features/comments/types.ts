/** Même limite que l'API (Comment.BodyMaxLength). */
export const COMMENT_BODY_MAX = 2000

/** Un commentaire, et pour un commentaire d'origine ses réponses (liste vide pour une réponse). */
export interface Comment {
  id: string
  parentId: string | null
  /** null quand le compte de l'auteur a été supprimé. */
  authorId: string | null
  authorEmail: string | null
  /** Position dans l'enregistrement, en millisecondes ; null pour un commentaire général. */
  positionMs: number | null
  body: string
  createdAt: string
  editedAt: string | null
  resolvedAt: string | null
  resolvedByEmail: string | null
  replies: Comment[]
}

/** Le fichier audio commenté. */
export interface CommentTarget {
  songId: string
  versionId: string
  fileId: string
}