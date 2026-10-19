import { request } from '@/shared/api/httpClient'
import { bandPath } from '@/shared/api/bandPath'
import type { SongRights, SongRightsInput, VersionIsrc } from '../types'

const songPath = (songId: string, suffix: string) => bandPath(`/songs/${songId}${suffix}`)

export const rightsApi = {
  get: (songId: string) => request<SongRights>(songPath(songId, '/rights')),
  /** Remplace l'ISWC et tous les crédits ; 422 si le total ne fait pas 100 %. */
  save: (songId: string, input: SongRightsInput) =>
    request<SongRights>(songPath(songId, '/rights'), { method: 'PUT', body: input }),
  /** Pose (ou retire, avec null) l'ISRC d'une version. */
  setIsrc: (songId: string, versionId: string, isrc: string | null) =>
    request<VersionIsrc>(songPath(songId, `/versions/${versionId}/isrc`), { method: 'PUT', body: { isrc } }),
  /** Adresses des exports CSV, pour un lien <a download> (le cookie de session suit). */
  songCsvUrl: (songId: string) => `/api${songPath(songId, '/rights.csv')}`,
  releaseCsvUrl: (releaseId: string) => `/api${bandPath(`/releases/${releaseId}/credits.csv`)}`,
}