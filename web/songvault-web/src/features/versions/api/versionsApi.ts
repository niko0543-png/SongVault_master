import { request } from '@/shared/api/httpClient'
import type { SongVersion, SongVersionInput, SongVersionStatus, SongVersionSummary } from '../types'

const base = (songId: string) => `/songs/${songId}/versions`

function list(songId: string) {
  return request<SongVersionSummary[]>(base(songId))
}

function get(songId: string, versionId: string) {
  return request<SongVersion>(`${base(songId)}/${versionId}`)
}

function create(songId: string, input: SongVersionInput) {
  return request<SongVersion>(base(songId), { method: 'POST', body: input })
}

function update(songId: string, versionId: string, input: SongVersionInput) {
  return request<void>(`${base(songId)}/${versionId}`, { method: 'PUT', body: input })
}

/**
 * Change uniquement le statut. PUT remplaçant TOUTE la version,
 * on renvoie tous les champs actuels — y compris bpm et key.
 */
async function changeStatus(songId: string, versionId: string, status: SongVersionStatus) {
  const current = await get(songId, versionId)
  await update(songId, versionId, {
    title: current.title,
    status,
    notes: current.notes,
    lyrics: current.lyrics,
    bpm: current.bpm,      // NOUVEAU : sans cette ligne, le BPM serait effacé
    key: current.key,      // NOUVEAU
  })
  return get(songId, versionId)
}

export const versionsApi = { list, get, create, update, changeStatus }