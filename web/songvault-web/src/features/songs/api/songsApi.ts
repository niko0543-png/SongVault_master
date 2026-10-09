import { request } from '@/shared/api/httpClient'
import { bandPath } from '@/shared/api/bandPath'
import type { PagedResult } from '@/shared/api/types'
import type { Song, SongInput } from '../types'
import type { SongVersionStatus } from '@/features/versions/types'

export interface SongListParams { page?: number; pageSize?: number; search?: string; status?: SongVersionStatus }

export const songsApi = {
  list: (params: SongListParams = {}) => {
    const query = new URLSearchParams({ page: String(params.page ?? 1), pageSize: String(params.pageSize ?? 50) })
    if (params.search) query.set('search', params.search)        // URLSearchParams encode les caractères spéciaux
    if (params.status) query.set('status', params.status)
    return request<PagedResult<Song>>(bandPath(`/songs?${query}`))
  },
  get: (id: string) => request<Song>(bandPath(`/songs/${id}`)),
  create: (input: SongInput) => request<Song>(bandPath('/songs'), { method: 'POST', body: input }),
  update: (id: string, input: SongInput) => request<void>(bandPath(`/songs/${id}`), { method: 'PUT', body: input }),
  remove: (id: string) => request<void>(bandPath(`/songs/${id}`), { method: 'DELETE' }),
}