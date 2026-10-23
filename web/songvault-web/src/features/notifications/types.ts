import { labels, t } from '@/shared/i18n'

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
const kindLabel = (kind: NotificationKind) => ({
  get title() { return t(`notifications.kinds.${kind}.title` as const) },
  get help() { return t(`notifications.kinds.${kind}.help` as const) },
})

export const KIND_LABELS: Readonly<Record<NotificationKind, { readonly title: string; readonly help: string }>> = {
  NewVersion: kindLabel('NewVersion'),
  Comment: kindLabel('Comment'),
  Mention: kindLabel('Mention'),
}

export const MODE_LABELS = labels<NotificationMode>({
  Immediate: 'notifications.modes.Immediate',
  Daily: 'notifications.modes.Daily',
  Off: 'notifications.modes.Off',
})