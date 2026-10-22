import { request } from '@/shared/api/httpClient'
import { bandPath } from '@/shared/api/bandPath'
import type { TabSync, TabSyncPoint } from './types'

const path = (songId: string, versionId: string, fileId: string) =>
  bandPath(`/songs/${songId}/versions/${versionId}/files/${fileId}/sync`)

/** #29 : le calage de la tablature ; undefined si elle n'est pas calée (204), ou si sa maquette est à la corbeille. */
function get(songId: string, versionId: string, fileId: string) {
  return request<TabSync | undefined>(path(songId, versionId, fileId))
}

/** Remplace le calage (Member ou Owner). */
function save(songId: string, versionId: string, fileId: string, audioFileId: string, points: TabSyncPoint[]) {
  return request<TabSync>(path(songId, versionId, fileId), { method: 'PUT', body: { audioFileId, points } })
}

/** Retire le calage : la tablature se joue de nouveau avec son propre son. */
function remove(songId: string, versionId: string, fileId: string) {
  return request<void>(path(songId, versionId, fileId), { method: 'DELETE' })
}

export const syncApi = { get, save, remove }