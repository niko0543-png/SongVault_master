import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { createMemoryHistory, createRouter } from 'vue-router'
import TrashPage from '../pages/TrashPage.vue'
import { trashApi } from '../api/trashApi'
import type { TrashItem } from '../types'
import { ApiError } from '@/shared/api/ApiError'
import { useConfirm } from '@/shared/composables/useConfirm'

vi.mock('../api/trashApi')

function item(over: Partial<TrashItem> = {}): TrashItem {
  return {
    kind: 'Song', id: 's1', songId: 's1', songTitle: 'Nocturne', versionNumber: null, versionTitle: null, fileName: null,
    sizeBytes: 4096, deletedAt: '2026-10-19T09:00:00Z', deletedById: 'u-ana', deletedByEmail: 'ana@test.local',
    purgeAt: '2026-11-18T09:00:00Z', ...over,
  }
}

async function mountPage(role: 'Owner' | 'Member' = 'Owner') {
  const Stub = { render: () => null }
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/b/:bandId/trash', name: 'trash', component: Stub }] })
  await router.push('/b/b1/trash')
  const wrapper = mount(TrashPage, {
    attachTo: document.body,
    global: {
      plugins: [router, createTestingPinia({
        createSpy: vi.fn,
        stubActions: false,          // bands.can() doit répondre selon le rôle
        initialState: {
          band: { bands: [{ id: 'b1', name: 'Les Rats', role }], activeId: 'b1', isLoaded: true },
          auth: { user: { id: 'u-moi', email: 'moi@test.local' } },
        },
      })],
    },
  })
  await flushPromises()
  return wrapper
}

describe('TrashPage', () => {
  beforeEach(() => {
    vi.mocked(trashApi.list).mockReset()
    vi.mocked(trashApi.restore).mockReset()
    vi.mocked(trashApi.remove).mockReset()
  })

  it('nomme chaque élément, qui l’a supprimé et ce qu’il occupe', async () => {
    vi.mocked(trashApi.list).mockResolvedValue([
      item({ kind: 'File', id: 'f1', versionNumber: 2, versionTitle: 'Maquette', fileName: 'prise.wav', deletedById: 'u-moi' }),
      item({ deletedByEmail: null }),
    ])
    const w = await mountPage()

    const rows = w.findAll('[data-testid="trash-item"]')
    expect(rows[0]!.text()).toContain('Nocturne — v2 — prise.wav')
    expect(rows[0]!.text()).toContain('Supprimé par vous')
    expect(rows[1]!.text()).toContain('Supprimé par un compte supprimé')
    expect(rows[1]!.text()).toContain('4 Ko')
    w.unmount()
  })

  it('restaure, retire la ligne et relit la corbeille', async () => {
    vi.mocked(trashApi.list).mockResolvedValueOnce([item()]).mockResolvedValueOnce([])
    vi.mocked(trashApi.restore).mockResolvedValue()
    const w = await mountPage('Member')

    await w.get('button.secondary').trigger('click')
    await flushPromises()

    expect(trashApi.restore).toHaveBeenCalledWith(expect.objectContaining({ kind: 'Song', id: 's1' }))
    expect(trashApi.list).toHaveBeenCalledTimes(2)
    expect(w.text()).toContain('La corbeille est vide.')
    w.unmount()
  })

  it('montre le refus d’une version dont le morceau est à la corbeille', async () => {
    vi.mocked(trashApi.list).mockResolvedValue([item({ kind: 'Version', id: 'v1', versionNumber: 1, versionTitle: 'Idée' })])
    vi.mocked(trashApi.restore).mockRejectedValue(new ApiError(409, {
      title: 'Conflit', status: 409, detail: 'Le morceau « Nocturne » est à la corbeille : restaurez-le, il revient avec cette version.',
    }))
    const w = await mountPage()

    await w.get('button.secondary').trigger('click')
    await flushPromises()

    expect(w.get('[role="alert"]').text()).toContain('restaurez-le')
    expect(w.findAll('[data-testid="trash-item"]')).toHaveLength(1)
    w.unmount()
  })

  it('« Supprimer définitivement » : Owner seulement, après confirmation', async () => {
    vi.mocked(trashApi.list).mockResolvedValue([item()])
    vi.mocked(trashApi.remove).mockResolvedValue()
    const member = await mountPage('Member')
    expect(member.find('button.danger').exists()).toBe(false)
    member.unmount()

    const owner = await mountPage('Owner')
    await owner.get('[data-testid="trash-item"] button.danger').trigger('click')
    await flushPromises()
    useConfirm().answer(true)
    await flushPromises()

    expect(trashApi.remove).toHaveBeenCalledWith(expect.objectContaining({ id: 's1' }))
    owner.unmount()
  })
})