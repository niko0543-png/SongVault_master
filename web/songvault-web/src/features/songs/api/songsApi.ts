import { request } from '@/shared/api/httpClient'
import type { PagedResult } from '@/shared/api/types'
import type { Song, SongInput } from '../types'

export const songsApi = {
  list: (page = 1, pageSize = 50) => request<PagedResult<Song>>(`/songs?page=${page}&pageSize=${pageSize}`),
  get: (id: string) => request<Song>(`/songs/${id}`),
  create: (input: SongInput) => request<Song>('/songs', { method: 'POST', body: input }),
  update: (id: string, input: SongInput) => request<void>(`/songs/${id}`, { method: 'PUT', body: input }),
  remove: (id: string) => request<void>(`/songs/${id}`, { method: 'DELETE' }),
}
