import { request } from '@/shared/api/httpClient'
import { bandPath } from '@/shared/api/bandPath'
import { putFile } from './putFile'
import type { FilePolicy, FileUploadTicket, SongFile } from '../types'

const base = (songId: string, versionId: string) => bandPath(`/songs/${songId}/versions/${versionId}/files`)

function contentUrl(songId: string, versionId: string, fileId: string, download = false) {
  return `/api${base(songId, versionId)}/${fileId}/content${download ? '?download=true' : ''}`
}

function policy() {
  return request<FilePolicy>('/files/policy')
}

/**
 * Envoi direct en trois temps (ADR 0012) : annonce, PUT du fichier à l'adresse reçue, confirmation.
 * Le même parcours sert en stockage local et avec Azure Blob : seule l'adresse change.
 */
async function upload(songId: string, versionId: string, file: File, onProgress?: (ratio: number) => void) {
  const path = `${base(songId, versionId)}/uploads`
  const ticket = await request<FileUploadTicket>(path, {
    method: 'POST',
    body: { fileName: file.name, sizeBytes: file.size },
  })
  await putFile(ticket.uploadUrl, ticket.headers, file, onProgress)
  return request<SongFile>(`${path}/${ticket.uploadId}/complete`, { method: 'POST' })
}

function remove(songId: string, versionId: string, fileId: string) {
  return request<void>(`${base(songId, versionId)}/${fileId}`, { method: 'DELETE' })
}

export const filesApi = { contentUrl, policy, upload, remove }