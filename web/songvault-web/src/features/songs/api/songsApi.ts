import { request } from '@/shared/api/httpClient'
import type { PagedResult } from '@/shared/api/types'
import type { Song, SongInput } from '../types'
import type { SongVersionStatus } from '@/features/versions/types'

export interface SongListParams { page?: number; pageSize?: number; search?: string; status?: SongVersionStatus }

export const songsApi = {
  list: (params: SongListParams = {}) => {
    const query = new URLSearchParams({ page: String(params.page ?? 1), pageSize: String(params.pageSize ?? 50) })
    if (params.search) query.set('search', params.search)        // URLSearchParams encode les caractères spéciaux
    if (params.status) query.set('status', params.status)
    return request<PagedResult<Song>>(`/songs?${query}`)
  },
  get: (id: string) => request<Song>(`/songs/${id}`),
  create: (input: SongInput) => request<Song>('/songs', { method: 'POST', body: input }),
  update: (id: string, input: SongInput) => request<void>(`/songs/${id}`, { method: 'PUT', body: input }),
  remove: (id: string) => request<void>(`/songs/${id}`, { method: 'DELETE' }),
}
