import { request } from '@/shared/api/httpClient'
import { bandPath } from '@/shared/api/bandPath'
import type { Release, ReleaseInput, ReleaseSummary, TrackChoice } from '../types'

const path = (suffix = '') => bandPath(`/releases${suffix}`)

export const releasesApi = {
  list: () => request<ReleaseSummary[]>(path()),
  get: (id: string) => request<Release>(path(`/${id}`)),
  create: (input: ReleaseInput) => request<Release>(path(), { method: 'POST', body: input }),
  update: (id: string, input: ReleaseInput) => request<void>(path(`/${id}`), { method: 'PUT', body: input }),
  remove: (id: string) => request<void>(path(`/${id}`), { method: 'DELETE' }),
  /** Remplace toutes les pistes par cette liste, dans cet ordre ; renvoie la sortie à jour. */
  setTracks: (id: string, tracks: TrackChoice[]) =>
    request<Release>(path(`/${id}/tracks`), { method: 'PUT', body: { tracks } }),
  uploadCover: (id: string, file: File) => {
    const form = new FormData()
    form.append('file', file)
    return request<void>(path(`/${id}/cover`), { method: 'PUT', body: form })
  },
  removeCover: (id: string) => request<void>(path(`/${id}/cover`), { method: 'DELETE' }),
  /** Adresse de l'image, pour un <img> ; version (updatedAt) change l'adresse après un nouvel envoi. */
  coverUrl: (id: string, version?: string) =>
    `/api${path(`/${id}/cover`)}${version ? `?v=${encodeURIComponent(version)}` : ''}`,
}

/** Ordre et versions actuels d'une sortie, au format attendu par setTracks. */
export function toChoices(release: Pick<Release, 'tracks'>): TrackChoice[] {
  return release.tracks.map((t) => ({ songId: t.songId, versionId: t.versionId }))
}