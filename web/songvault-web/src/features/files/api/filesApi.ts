import { request } from '@/shared/api/httpClient'
import { bandPath } from '@/shared/api/bandPath'
import { putFile } from './putFile'
import type { AudioPreviewStatus, AudioWaveform, FilePolicy, FileScanStatus, FileUploadTicket, SongFile } from '../types'

const base = (songId: string, versionId: string) => bandPath(`/songs/${songId}/versions/${versionId}/files`)

function contentUrl(songId: string, versionId: string, fileId: string, download = false) {
  return `/api${base(songId, versionId)}/${fileId}/content${download ? '?download=true' : ''}`
}

/** #15 : ce que le lecteur charge : l'aperçu MP3 quand il est prêt, sinon l'original. */
function listenUrl(songId: string, versionId: string, fileId: string) {
  return `/api${base(songId, versionId)}/${fileId}/listen`
}

function waveform(songId: string, versionId: string, fileId: string) {
  return request<AudioWaveform>(`${base(songId, versionId)}/${fileId}/waveform`)
}

function retryPreview(songId: string, versionId: string, fileId: string) {
  return request<{ status: AudioPreviewStatus }>(`${base(songId, versionId)}/${fileId}/preview`, { method: 'POST' })
}

/** #17 : relance une analyse antivirus en échec. */
function retryScan(songId: string, versionId: string, fileId: string) {
  return request<{ status: FileScanStatus }>(`${base(songId, versionId)}/${fileId}/scan`, { method: 'POST' })
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

export const filesApi = { contentUrl, listenUrl, waveform, retryPreview, retryScan, policy, upload, remove }