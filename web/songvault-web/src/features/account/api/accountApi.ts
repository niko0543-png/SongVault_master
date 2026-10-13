import { request } from '@/shared/api/httpClient'
import type { DataExport } from '../types'

/** Lien simple, sans fetch : le navigateur télécharge le ZIP avec le cookie de session. */
export const EXPORT_DOWNLOAD_URL = '/api/me/export/download'

export const accountApi = {
  /** Dernière archive ; null s'il n'y en a pas, ou si elle a expiré (204). */
  latestExport: async () => (await request<DataExport | undefined>('/me/export')) ?? null,
  requestExport: () => request<DataExport>('/me/export', { method: 'POST' }),
  deleteAccount: (password: string) => request<void>('/me/delete', { method: 'POST', body: { password } }),
}