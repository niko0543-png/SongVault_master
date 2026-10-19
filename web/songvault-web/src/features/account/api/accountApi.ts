import { request } from '@/shared/api/httpClient'
import type { AccountEvent, DataExport, TwoFactorSetup, TwoFactorStatus } from '../types'

/** Lien simple, sans fetch : le navigateur télécharge le ZIP avec le cookie de session. */
export const EXPORT_DOWNLOAD_URL = '/api/me/export/download'

export const accountApi = {
  /** Dernière archive ; null s'il n'y en a pas, ou si elle a expiré (204). */
  latestExport: async () => (await request<DataExport | undefined>('/me/export')) ?? null,
  requestExport: () => request<DataExport>('/me/export', { method: 'POST' }),
  deleteAccount: (password: string) => request<void>('/me/delete', { method: 'POST', body: { password } }),

  // ---- #19 : double authentification et historique de connexion ----
  twoFactorStatus: () => request<TwoFactorStatus>('/me/two-factor'),
  setupTwoFactor: () => request<TwoFactorSetup>('/me/two-factor/setup', { method: 'POST' }),
  /** Renvoie les 10 codes de secours, montrés une seule fois. */
  enableTwoFactor: async (code: string) =>
    (await request<{ recoveryCodes: string[] }>('/me/two-factor/enable', { method: 'POST', body: { code } })).recoveryCodes,
  disableTwoFactor: (password: string) => request<void>('/me/two-factor/disable', { method: 'POST', body: { password } }),
  regenerateRecoveryCodes: async (password: string) =>
    (await request<{ recoveryCodes: string[] }>('/me/two-factor/recovery-codes', { method: 'POST', body: { password } })).recoveryCodes,
  forgetBrowser: () => request<void>('/me/two-factor/forget-browser', { method: 'POST' }),
  accountEvents: () => request<AccountEvent[]>('/me/account-events'),
}