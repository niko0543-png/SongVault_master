import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import DataExportSection from '../components/DataExportSection.vue'
import { accountApi } from '../api/accountApi'
import { ApiError } from '@/shared/api/ApiError'
import type { DataExport } from '../types'

vi.mock('../api/accountApi', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../api/accountApi')>()),
  accountApi: { latestExport: vi.fn(), requestExport: vi.fn(), deleteAccount: vi.fn() },
}))

const pending: DataExport = {
  id: 'e1', status: 'Pending', requestedAt: '2026-10-12T09:00:00Z', completedAt: null, expiresAt: null, sizeBytes: null,
}
const ready: DataExport = {
  ...pending, status: 'Ready', completedAt: '2026-10-12T09:01:00Z', expiresAt: '2026-10-19T09:01:00Z', sizeBytes: 2048,
}

let wrapper: VueWrapper | undefined

async function mountSection() {
  wrapper = mount(DataExportSection)
  await flushPromises()
  return wrapper
}

describe('DataExportSection', () => {
  beforeEach(() => {
    vi.mocked(accountApi.latestExport).mockReset().mockResolvedValue(null)
    vi.mocked(accountApi.requestExport).mockReset().mockResolvedValue(pending)
  })
  afterEach(() => wrapper?.unmount())                     // arrête le rafraîchissement toutes les 5 s

  it('archive prête : affiche sa taille et le lien de téléchargement', async () => {
    vi.mocked(accountApi.latestExport).mockResolvedValue(ready)

    const w = await mountSection()

    expect(w.text()).toContain('Archive prête (2 Ko)')
    expect(w.get('a[download]').attributes('href')).toBe('/api/me/export/download')
  })

  it('demande une archive, puis attend sa préparation', async () => {
    const w = await mountSection()

    await w.get('button').trigger('click')
    await flushPromises()

    expect(accountApi.requestExport).toHaveBeenCalledOnce()
    expect(w.text()).toContain('Préparation en cours…')
    expect(w.get('button').attributes('disabled')).toBeDefined()
  })

  it('affiche le refus du serveur', async () => {
    vi.mocked(accountApi.requestExport).mockRejectedValue(
      new ApiError(409, { title: 'Conflit', status: 409, detail: 'Une archive est déjà en préparation.' }))
    const w = await mountSection()

    await w.get('button').trigger('click')
    await flushPromises()

    expect(w.get('[role="alert"]').text()).toBe('Une archive est déjà en préparation.')
  })
})