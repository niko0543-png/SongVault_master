import { request } from '@/shared/api/httpClient'
import type { Band } from '../types'

export const bandsApi = {
  mine: () => request<Band[]>('/bands'),
  create: (name: string) => request<Band>('/bands', { method: 'POST', body: { name } }),
  rename: (id: string, name: string) => request<void>(`/bands/${id}`, { method: 'PUT', body: { name } }),
}