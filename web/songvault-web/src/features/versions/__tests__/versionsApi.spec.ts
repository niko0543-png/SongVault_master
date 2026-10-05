import { beforeEach, describe, expect, it, vi } from 'vitest'
import { request } from '@/shared/api/httpClient'
import { versionsApi } from '../api/versionsApi'
import type { SongVersion } from '../types'

vi.mock('@/shared/api/httpClient')     // Vitest remplace automatiquement request par un faux

const current: SongVersion = {
  id: 'v1', songId: 's1', number: 2, title: 'Arrangement cordes', status: 'Demo',
  notes: 'Capo 2', lyrics: null, bpm: 92, key: 'F#m',
  createdAt: '2026-10-19T09:00:00Z', updatedAt: '2026-10-19T09:00:00Z', files: [],
}

describe('versionsApi.changeStatus', () => {
  beforeEach(() => vi.mocked(request).mockReset())

  it('conserve le BPM et la tonalité en changeant le statut', async () => {
    // GET renvoie la version actuelle ; PUT ne renvoie rien (204)
    vi.mocked(request).mockImplementation(async (_path, options) =>
      (options?.method === 'PUT' ? undefined : current) as never)

    await versionsApi.changeStatus('s1', 'v1', 'Studio')

    const put = vi.mocked(request).mock.calls.find(([, options]) => options?.method === 'PUT')
    expect(put?.[1]?.body).toEqual({
      title: 'Arrangement cordes', status: 'Studio', notes: 'Capo 2', lyrics: null,
      bpm: 92, key: 'F#m',
    })
  })
})