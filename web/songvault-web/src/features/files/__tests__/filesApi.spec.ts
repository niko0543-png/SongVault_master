import { beforeEach, describe, expect, it, vi } from 'vitest'
import { filesApi } from '../api/filesApi'
import { putFile } from '../api/putFile'
import { request } from '@/shared/api/httpClient'

vi.mock('@/shared/api/bandPath', () => ({ bandPath: (path: string) => `/bands/b1${path}` }))
vi.mock('@/shared/api/httpClient', () => ({ request: vi.fn() }))
vi.mock('../api/putFile', () => ({ putFile: vi.fn() }))

const ticket = {
  uploadId: 'u1',
  uploadUrl: 'https://stockage/f.mp3?sig=x',
  headers: { 'x-ms-blob-type': 'BlockBlob' },
  expiresAt: '2026-10-13T12:00:00Z',
}
const file = new File(['ID3'], 'maquette.mp3')

describe('filesApi.upload', () => {
  beforeEach(() => vi.resetAllMocks())

  it("annonce l'envoi, envoie le fichier à l'adresse reçue, puis confirme", async () => {
    vi.mocked(request).mockResolvedValueOnce(ticket).mockResolvedValueOnce({ id: 'f1' })
    vi.mocked(putFile).mockResolvedValue()
    const onProgress = vi.fn()

    const result = await filesApi.upload('s1', 'v1', file, onProgress)

    expect(request).toHaveBeenNthCalledWith(1, '/bands/b1/songs/s1/versions/v1/files/uploads', {
      method: 'POST',
      body: { fileName: 'maquette.mp3', sizeBytes: 3 },
    })
    expect(putFile).toHaveBeenCalledWith(ticket.uploadUrl, ticket.headers, file, onProgress)
    expect(request).toHaveBeenNthCalledWith(2, '/bands/b1/songs/s1/versions/v1/files/uploads/u1/complete', {
      method: 'POST',
    })
    expect(result).toEqual({ id: 'f1' })
  })

  it("ne confirme pas un envoi qui a échoué", async () => {
    vi.mocked(request).mockResolvedValueOnce(ticket)
    vi.mocked(putFile).mockRejectedValue(new Error('coupure'))

    await expect(filesApi.upload('s1', 'v1', file)).rejects.toThrow('coupure')

    expect(request).toHaveBeenCalledTimes(1)
  })
})