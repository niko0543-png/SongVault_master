import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { createMemoryHistory, createRouter } from 'vue-router'
import SetlistListPage from '../pages/SetlistListPage.vue'
import { setlistsApi } from '../api/setlistsApi'
import type { Setlist, SetlistSummary } from '../types'
import type { BandRole } from '@/features/bands/types'

vi.mock('../api/setlistsApi', () => ({ setlistsApi: { list: vi.fn(), create: vi.fn() } }))

const setlists: SetlistSummary[] = [
  { id: 's2', name: 'Répétition', date: null, venue: null, itemCount: 1 },
  { id: 's1', name: 'Concert au Ferrailleur', date: '2026-11-28', venue: 'Nantes', itemCount: 14 },
]

async function mountPage(role: BandRole) {
  const Stub = { render: () => null }
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/b/:bandId/setlists', name: 'setlists', component: Stub },
      { path: '/b/:bandId/setlists/:setlistId', name: 'setlist-detail', component: Stub },
    ],
  })
  await router.push('/b/b1/setlists')
  const wrapper = mount(SetlistListPage, {
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

describe('SetlistListPage', () => {
  beforeEach(() => vi.mocked(setlistsApi.list).mockReset().mockResolvedValue(setlists))

  it('affiche date, lieu et nombre de morceaux ; un Guest ne voit pas le formulaire', async () => {
    const { wrapper } = await mountPage('Guest')

    const items = wrapper.findAll('[data-testid="setlist-item"]')
    expect(items[0]!.text()).toContain('Sans date · 1 morceau')
    expect(items[1]!.text()).toContain('samedi 28 novembre 2026 · Nantes · 14 morceaux')
    expect(wrapper.find('form').exists()).toBe(false)
  })

  it('créer une setlist ouvre son détail', async () => {
    vi.mocked(setlistsApi.create).mockResolvedValue({ id: 's3' } as Setlist)
    const { wrapper, router } = await mountPage('Member')

    await wrapper.get('#setlist-name').setValue('  Fête de la musique ')
    await wrapper.get('#setlist-venue').setValue('Rennes')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(setlistsApi.create).toHaveBeenCalledWith({ name: 'Fête de la musique', date: null, venue: 'Rennes' })
    expect(router.currentRoute.value.fullPath).toBe('/b/b1/setlists/s3')
  })
})