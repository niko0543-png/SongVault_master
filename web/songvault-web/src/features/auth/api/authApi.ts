import { request } from '@/shared/api/httpClient'
import type { Credentials, CurrentUser } from '../types'

export const authApi = {
  register: (credentials: Credentials) => request<void>('/auth/register', { method: 'POST', body: credentials }),
  login: (credentials: Credentials) =>
    request<void>('/auth/login?useCookies=true', { method: 'POST', body: credentials }),
  logout: () => request<void>('/auth/logout', { method: 'POST' }),
  me: () => request<CurrentUser>('/auth/me'),
    /** #26 : la langue choisie suit le compte d'un appareil à l'autre. */
  saveLocale: (locale: string) => request<void>('/me/locale', { method: 'PUT', body: { locale } }),

  // ---- #4 : endpoints fournis par MapIdentityApi ----
  /** params : userId, code (et changedEmail) repris du lien reçu par e-mail. */
  confirmEmail: (params: URLSearchParams) => request<void>(`/auth/confirmEmail?${params}`),
  resendConfirmation: (email: string) =>
    request<void>('/auth/resendConfirmationEmail', { method: 'POST', body: { email } }),
  forgotPassword: (email: string) => request<void>('/auth/forgotPassword', { method: 'POST', body: { email } }),
  /** Vérifie le lien à son ouverture, sans le consommer : 400 InvalidToken s'il a expiré ou déjà servi. */
  checkResetCode: (email: string, resetCode: string) =>
    request<void>('/auth/checkResetCode', { method: 'POST', body: { email, resetCode } }),
  resetPassword: (email: string, resetCode: string, newPassword: string) =>
    request<void>('/auth/resetPassword', { method: 'POST', body: { email, resetCode, newPassword } }),
  /** #19 : par /api/me (POST /auth/manage/info est fermé) ; cette session reste ouverte. */
  changePassword: (currentPassword: string, newPassword: string) =>
    request<void>('/me/password', { method: 'POST', body: { currentPassword, newPassword } }),
}