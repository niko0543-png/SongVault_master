import { request } from '@/shared/api/httpClient'
import { bandPath } from '@/shared/api/bandPath'
import type { Stem, StemInput } from '../types'

const path = (songId: string, versionId: string) => bandPath(`/songs/${songId}/versions/${versionId}/stems`)

export const stemsApi = {
  list: (songId: string, versionId: string) => request<Stem[]>(path(songId, versionId)),
  /** Remplace toutes les pistes, dans l'ordre donné ; liste vide = plus de mélangeur. */
  save: (songId: string, versionId: string, stems: StemInput[]) =>
    request<Stem[]>(path(songId, versionId), { method: 'PUT', body: { stems } }),
}