import { request } from '@/shared/api/httpClient'
import type { NotificationKind, NotificationMode, NotificationPreference, UnsubscribeResult } from '../types'

export const notificationsApi = {
  preferences: () => request<NotificationPreference[]>('/me/notifications'),
  update: (changes: { kind: NotificationKind; mode: NotificationMode }[]) =>
    request<NotificationPreference[]>('/me/notifications', { method: 'PUT', body: changes }),
  /** Sans connexion : le jeton du lien suffit. */
  unsubscribe: (token: string) =>
    request<UnsubscribeResult>(`/notifications/unsubscribe?token=${encodeURIComponent(token)}`, { method: 'POST' }),
}