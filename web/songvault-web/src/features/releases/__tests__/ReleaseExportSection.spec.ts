import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import ReleaseExportSection from '../components/ReleaseExportSection.vue'
import { releaseExportsApi } from '../api/releaseExportsApi'
import { ApiError } from '@/shared/api/ApiError'
import type { ReleaseExport } from '../types'

// archiveUrl reste la vraie fonction : l'adresse dépend du groupe actif
vi.mock('../api/releaseExportsApi', async (importOriginal) => {
  const original = await importOriginal<typeof import('../api/releaseExportsApi')>()
  return { releaseExportsApi: { ...original.releaseExportsApi, latest: vi.fn(), request: vi.fn() } }
})

const pending: ReleaseExport = {
  id: 'x1', status: 'Pending', requestedAt: '2026-10-19T09:00:00Z', completedAt: null, expiresAt: null, sizeBytes: null,
}
const ready: ReleaseExport = {
  ...pending, status: 'Ready', completedAt: '2026-10-19T09:01:00Z', expiresAt: '2026-10-20T09:01:00Z', sizeBytes: 3 * 1024 * 1024,
}

let wrapper: VueWrapper | undefined

async function mountSection(canWrite = true) {
  wrapper = mount(ReleaseExportSection, {
    props: { releaseId: 'r1', canWrite },
    global: {
      plugins: [createTestingPinia({
        createSpy: vi.fn,
        initialState: { band: { bands: [{ id: 'b1', name: 'Les Rats', role: 'Member' }], activeId: 'b1', isLoaded: true } },
      })],
    },
  })
  await flushPromises()
  return wrapper
}

describe('ReleaseExportSection', () => {
  beforeEach(() => {
    vi.useRealTimers()
    vi.mocked(releaseExportsApi.latest).mockReset().mockResolvedValue(null)
    vi.mocked(releaseExportsApi.request).mockReset().mockResolvedValue(pending)
  })
  afterEach(() => {
    wrapper?.unmount()                                      // arrête le rafraîchissement toutes les 5 s
    vi.useRealTimers()
  })

  it('archive prête : affiche sa taille, son échéance et le lien vers l’archive', async () => {
    vi.mocked(releaseExportsApi.latest).mockResolvedValue(ready)

    const w = await mountSection()

    expect(releaseExportsApi.latest).toHaveBeenCalledWith('r1')
    expect(w.text()).toContain('Archive prête (3 Mo)')
    expect(w.get('a[download]').attributes('href')).toBe('/api/bands/b1/releases/r1/exports/x1/archive')
  })

  it('demande l’archive, puis redemande son état jusqu’à ce qu’elle soit prête', async () => {
    vi.useFakeTimers()
    const w = await mountSection()

    await w.get('button').trigger('click')
    await flushPromises()
    expect(releaseExportsApi.request).toHaveBeenCalledWith('r1')
    expect(w.text()).toContain('Préparation en cours…')
    expect(w.get('button').attributes('disabled')).toBeDefined()

    vi.mocked(releaseExportsApi.latest).mockResolvedValue(ready)
    await vi.advanceTimersByTimeAsync(5000)
    await flushPromises()

    expect(w.text()).toContain('Archive prête')
    expect(w.get('button').text()).toBe('Préparer une nouvelle archive')
  })

  it('affiche le refus du serveur, par exemple un fichier encore en analyse', async () => {
    vi.mocked(releaseExportsApi.request).mockRejectedValue(new ApiError(409, {
      title: 'Conflit', status: 409, detail: 'Fichiers pas encore disponibles : « aube.wav » (piste 1).',
    }))
    const w = await mountSection()

    await w.get('button').trigger('click')
    await flushPromises()

    expect(w.get('[role="alert"]').text()).toContain('« aube.wav » (piste 1)')
  })

  it('un invité voit l’archive prête mais ne peut pas en demander une', async () => {
    vi.mocked(releaseExportsApi.latest).mockResolvedValue(ready)

    const w = await mountSection(false)

    expect(w.find('a[download]').exists()).toBe(true)
    expect(w.find('button').exists()).toBe(false)
  })

  it('après un échec, propose de réessayer', async () => {
    vi.mocked(releaseExportsApi.latest).mockResolvedValue({ ...pending, status: 'Failed', completedAt: '2026-10-19T09:10:00Z' })

    const w = await mountSection()

    expect(w.text()).toContain('La préparation a échoué')
    expect(w.get('button').attributes('disabled')).toBeUndefined()
  })
})