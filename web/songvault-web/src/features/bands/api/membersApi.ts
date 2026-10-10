import { request } from '@/shared/api/httpClient'
import { bandPath } from '@/shared/api/bandPath'
import type { BandMember, BandRole } from '../types'

export const membersApi = {
  list: () => request<BandMember[]>(bandPath('/members')),
  changeRole: (userId: string, role: BandRole) =>
    request<void>(bandPath(`/members/${encodeURIComponent(userId)}/role`), { method: 'PUT', body: { role } }),
  remove: (userId: string) =>
    request<void>(bandPath(`/members/${encodeURIComponent(userId)}`), { method: 'DELETE' }),
  leave: () => request<void>(bandPath('/members/me'), { method: 'DELETE' }),
}