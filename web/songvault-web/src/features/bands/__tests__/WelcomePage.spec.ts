import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import WelcomePage from '../pages/WelcomePage.vue'
import { bandsApi } from '../api/bandsApi'
import { useBandStore } from '../stores/bandStore'

vi.mock('../api/bandsApi')     // aucun appel réseau

async function mountPage() {
  const Stub = { render: () => null }
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/bienvenue', name: 'welcome', component: Stub },
      { path: '/b/:bandId/songs', name: 'songs', component: Stub },
      { path: '/formules', name: 'plans', component: Stub },
    ],
  })
  await router.push('/bienvenue')
  const pinia = createPinia()
  setActivePinia(pinia)
  const wrapper = mount(WelcomePage, { global: { plugins: [router, pinia] } })
  return { wrapper, router }
}

describe('WelcomePage', () => {
  beforeEach(() => {
    vi.mocked(bandsApi.create).mockReset()
  })

  it('crée le premier groupe avec le nom saisi puis ouvre ses morceaux', async () => {
    vi.mocked(bandsApi.create).mockResolvedValue({ id: 'b1', name: 'Les Nocturnes', role: 'Owner' })
    const { wrapper, router } = await mountPage()

    expect(wrapper.text()).toContain('premier groupe')
    await wrapper.get('input').setValue('  Les Nocturnes ')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(bandsApi.create).toHaveBeenCalledWith('Les Nocturnes')
    expect(useBandStore().has('b1')).toBe(true)
    expect(router.currentRoute.value.fullPath).toBe('/b/b1/songs')
  })

  it("n'envoie rien tant que le nom est vide", async () => {
    const { wrapper } = await mountPage()

    await wrapper.get('form').trigger('submit')

    expect(bandsApi.create).not.toHaveBeenCalled()
    expect(wrapper.get('button[type="submit"]').attributes('disabled')).toBeDefined()
  })
})
