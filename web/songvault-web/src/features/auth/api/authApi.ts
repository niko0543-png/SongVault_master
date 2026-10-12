import { request } from '@/shared/api/httpClient'
import type { Credentials, CurrentUser } from '../types'

export const authApi = {
  register: (credentials: Credentials) => request<void>('/auth/register', { method: 'POST', body: credentials }),
  login: (credentials: Credentials) =>
    request<void>('/auth/login?useCookies=true', { method: 'POST', body: credentials }),
  logout: () => request<void>('/auth/logout', { method: 'POST' }),
  me: () => request<CurrentUser>('/auth/me'),

  // ---- #4 : endpoints fournis par MapIdentityApi ----
  /** params : userId, code (et changedEmail) repris du lien reçu par e-mail. */
  confirmEmail: (params: URLSearchParams) => request<void>(`/auth/confirmEmail?${params}`),
  resendConfirmation: (email: string) =>
    request<void>('/auth/resendConfirmationEmail', { method: 'POST', body: { email } }),
  forgotPassword: (email: string) => request<void>('/auth/forgotPassword', { method: 'POST', body: { email } }),
  resetPassword: (email: string, resetCode: string, newPassword: string) =>
    request<void>('/auth/resetPassword', { method: 'POST', body: { email, resetCode, newPassword } }),
  changePassword: (oldPassword: string, newPassword: string) =>
    request<void>('/auth/manage/info', { method: 'POST', body: { oldPassword, newPassword } }),
}