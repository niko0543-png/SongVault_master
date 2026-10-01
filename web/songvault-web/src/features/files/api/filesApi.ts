import { request } from '@/shared/api/httpClient'
import type { FilePolicy, SongFile } from '../types'

const base = (songId: string, versionId: string) => `/songs/${songId}/versions/${versionId}/files`

function contentUrl(songId: string, versionId: string, fileId: string, download = false) {
  return `/api${base(songId, versionId)}/${fileId}/content${download ? '?download=true' : ''}`
}

function policy() {
  return request<FilePolicy>('/files/policy')
}

function upload(songId: string, versionId: string, file: File) {
  const form = new FormData()
  form.append('file', file)                    // "file" = nom du paramètre IFormFile côté C#
  return request<SongFile>(base(songId, versionId), { method: 'POST', body: form })
}

function remove(songId: string, versionId: string, fileId: string) {
  return request<void>(`${base(songId, versionId)}/${fileId}`, { method: 'DELETE' })
}

export const filesApi = { contentUrl, policy, upload, remove }
