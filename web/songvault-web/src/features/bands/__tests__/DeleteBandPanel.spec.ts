import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import DeleteBandPanel from '../components/DeleteBandPanel.vue'
import { bandsApi } from '../api/bandsApi'
import { useBandStore } from '../stores/bandStore'
import { ApiError } from '@/shared/api/ApiError'

vi.mock('../api/bandsApi')     // aucun appel réseau

async function mountPanel(memberCount = 1) {
  const Stub = { render: () => null }
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'home', component: Stub },
      { path: '/b/:bandId/members', name: 'band-members', component: Stub },
    ],
  })
  await router.push('/b/b1/members')
  const pinia = createPinia()
  setActivePinia(pinia)
  const bands = useBandStore()
  bands.bands = [{ id: 'b1', name: 'Mauvais compte', role: 'Owner' }]
  bands.isLoaded = true
  bands.setActive('b1')
  const wrapper = mount(DeleteBandPanel, { props: { memberCount }, global: { plugins: [router, pinia] } })
  return { wrapper, router, bands }
}

describe('DeleteBandPanel', () => {
  beforeEach(() => {
    vi.mocked(bandsApi.remove).mockReset()
  })

  it('reste désactivé tant que le nom exact du groupe n’est pas recopié', async () => {
    const { wrapper } = await mountPanel()
    const button = wrapper.get('button')

    expect(button.attributes('disabled')).toBeDefined()
    await wrapper.get('input').setValue('mauvais compte')
    expect(button.attributes('disabled')).toBeDefined()
    await wrapper.get('input').setValue(' Mauvais compte ')
    expect(button.attributes('disabled')).toBeUndefined()
  })

  it('supprime le groupe, le retire de la liste et revient à l’accueil', async () => {
    vi.mocked(bandsApi.remove).mockResolvedValue(undefined)
    const { wrapper, router, bands } = await mountPanel()

    await wrapper.get('input').setValue('Mauvais compte')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(bandsApi.remove).toHaveBeenCalledWith('b1', 'Mauvais compte')
    expect(bands.has('b1')).toBe(false)
    expect(bands.isLoaded).toBe(false)             // plus aucun groupe : la page Bienvenue suivra
    expect(router.currentRoute.value.name).toBe('home')
  })

  it('annonce les autres membres et affiche le refus de l’API', async () => {
    vi.mocked(bandsApi.remove).mockRejectedValue(new ApiError(409, {
      title: 'Conflit', status: 409,
      detail: 'Ce groupe a un abonnement en cours : résiliez-le depuis la page Facturation, puis supprimez le groupe.',
    }))
    const { wrapper, bands } = await mountPanel(3)

    expect(wrapper.text()).toContain('2 autres membres perdront')
    await wrapper.get('input').setValue('Mauvais compte')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.get('[role="alert"]').text()).toContain('abonnement en cours')
    expect(bands.has('b1')).toBe(true)
  })
})
