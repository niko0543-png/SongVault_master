import { request } from '@/shared/api/httpClient'
import type { Band } from '../types'

export const bandsApi = {
  mine: () => request<Band[]>('/bands'),
  create: (name: string) => request<Band>('/bands', { method: 'POST', body: { name } }),
  rename: (id: string, name: string) => request<void>(`/bands/${id}`, { method: 'PUT', body: { name } }),
  /** Suppression définitive (ADR 0048) : l'Owner recopie le nom du groupe pour confirmer. */
  remove: (id: string, confirmName: string) =>
    request<void>(`/bands/${id}`, { method: 'DELETE', body: { confirmName } }),
}