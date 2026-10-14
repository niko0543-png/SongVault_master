export type NotificationKind = 'NewVersion' | 'Comment' | 'Mention'
export type NotificationMode = 'Off' | 'Immediate' | 'Daily'

/** Mode d'un type d'e-mail (GET /api/me/notifications) ; isDefault : rien de choisi encore. */
export interface NotificationPreference {
  kind: NotificationKind
  mode: NotificationMode
  isDefault: boolean
}

/** Ce qu'a coupé un lien de désinscription ; kind null : tous les types. */
export interface UnsubscribeResult {
  kind: NotificationKind | null
}

/** Libellés, dans l'ordre d'affichage. */
export const KIND_LABELS: Record<NotificationKind, { title: string; help: string }> = {
  NewVersion: { title: 'Nouvelle version', help: 'Un membre ajoute une version à un morceau du groupe.' },
  Comment: { title: 'Commentaires et réponses', help: 'Un membre commente un fichier ou répond à un commentaire.' },
  Mention: { title: 'Mentions', help: 'Un membre écrit « @ » suivi de votre adresse dans un commentaire.' },
}

export const MODE_LABELS: Record<NotificationMode, string> = {
  Immediate: 'Tout de suite',
  Daily: 'Dans le résumé du matin',
  Off: 'Jamais',
}