import { request } from '@/shared/api/httpClient'
import type { Credentials, CurrentUser } from '../types'

export const authApi = {
  register: (credentials: Credentials) => request<void>('/auth/register', { method: 'POST', body: credentials }),
  login: (credentials: Credentials) =>
    request<void>('/auth/login?useCookies=true', { method: 'POST', body: credentials }),
  logout: () => request<void>('/auth/logout', { method: 'POST' }),
  me: () => request<CurrentUser>('/auth/me'),
}