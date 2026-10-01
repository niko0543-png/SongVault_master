import { request } from '@/shared/api/httpClient'
import type { SongVersionSummary } from '../types'

export const versionsApi = {
  list: (songId: string) => request<SongVersionSummary[]>(`/songs/${songId}/versions`),
}
