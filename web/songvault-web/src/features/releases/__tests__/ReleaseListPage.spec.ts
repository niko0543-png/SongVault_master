import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { createMemoryHistory, createRouter } from 'vue-router'
import ReleaseListPage from '../pages/ReleaseListPage.vue'
import { releasesApi } from '../api/releasesApi'
import type { Release, ReleaseSummary } from '../types'
import type { BandRole } from '@/features/bands/types'

vi.mock('../api/releasesApi', () => ({ releasesApi: { list: vi.fn(), create: vi.fn(), coverUrl: () => '/cover' } }))

const releases: ReleaseSummary[] = [
  { id: 'r2', title: 'Démos 2027', type: 'EP', releaseDate: null, hasCover: false, trackCount: 1 },
  { id: 'r1', title: 'Nuits blanches', type: 'Album', releaseDate: '2026-11-20', hasCover: true, trackCount: 9 },
]

async function mountPage(role: BandRole) {
  const Stub = { render: () => null }
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/b/:bandId/releases', name: 'releases', component: Stub },
      { path: '/b/:bandId/releases/:releaseId', name: 'release-detail', component: Stub },
    ],
  })
  await router.push('/b/b1/releases')
  const wrapper = mount(ReleaseListPage, {
    global: {
      plugins: [router, createTestingPinia({
        createSpy: vi.fn,
        stubActions: false,
        initialState: { band: { bands: [{ id: 'b1', name: 'Les Rats', role }], activeId: 'b1', isLoaded: true } },
      })],
    },
  })
  await flushPromises()
  return { wrapper, router }
}

describe('ReleaseListPage', () => {
  beforeEach(() => vi.mocked(releasesApi.list).mockReset().mockResolvedValue(releases))

  it('affiche type, date et nombre de pistes ; un Guest ne voit pas le formulaire', async () => {
    const { wrapper } = await mountPage('Guest')

    const items = wrapper.findAll('[data-testid="release-item"]')
    expect(items[0]!.text()).toContain('EP · Date à fixer · 1 piste')
    expect(items[1]!.text()).toContain('Album · 20 novembre 2026 · 9 pistes')
    expect(wrapper.find('form').exists()).toBe(false)
  })

  it('crée une sortie puis ouvre son détail', async () => {
    vi.mocked(releasesApi.create).mockResolvedValue({ id: 'r3' } as Release)
    const { wrapper, router } = await mountPage('Member')

    await wrapper.get('#release-title').setValue('  Aube  ')
    await wrapper.get('#release-type').setValue('Single')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(releasesApi.create).toHaveBeenCalledWith({ title: 'Aube', type: 'Single', releaseDate: null })
    expect(router.currentRoute.value.fullPath).toBe('/b/b1/releases/r3')
  })
})