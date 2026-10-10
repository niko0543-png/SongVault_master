import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { createMemoryHistory, createRouter, RouterLink } from 'vue-router'
import InvitePage from '../pages/InvitePage.vue'
import { invitationsApi } from '../api/invitationsApi'
import type { InvitationPreview } from '../types'

// Remplace le module entier : aucun appel réseau pendant les tests
vi.mock('../api/invitationsApi')

const preview: InvitationPreview = {
  bandName: 'Les Nocturnes', email: 'ana@test.local', role: 'Member', expiresAt: '2026-10-16T09:00:00Z',
}

/** Monte la page sur /invite/tok, avec ou sans utilisateur connecté. */
async function mountPage(email: string | null, query = '') {
  const Stub = { render: () => null }
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/invite/:token', name: 'invite', component: Stub },
      { path: '/login', name: 'login', component: Stub },
      { path: '/register', name: 'register', component: Stub },
      { path: '/b/:bandId/songs', name: 'songs', component: Stub },
      { path: '/:pathMatch(.*)*', component: Stub },
    ],
  })
  await router.push(`/invite/tok${query}`)
  const user = email ? { id: 'u1', email } : null
  const wrapper = mount(InvitePage, {
    props: { token: 'tok' },
    global: {
      plugins: [router, createTestingPinia({ createSpy: vi.fn, initialState: { auth: { user, isLoaded: true } } })],
    },
  })
  await flushPromises()
  return { wrapper, router }
}

describe('InvitePage', () => {
  beforeEach(() => {
    vi.mocked(invitationsApi.preview).mockReset().mockResolvedValue(preview)
    vi.mocked(invitationsApi.accept).mockReset().mockResolvedValue({ id: 'b2', name: 'Les Nocturnes', role: 'Member' })
  })

  it("sans compte : propose l'inscription avec l'adresse et le retour sur l'invitation", async () => {
    const { wrapper } = await mountPage(null)

    expect(wrapper.text()).toContain('Les Nocturnes')
    const [register] = wrapper.findAllComponents(RouterLink)
    expect(register?.props('to')).toEqual({
      name: 'register', query: { email: 'ana@test.local', redirect: '/invite/tok?accept=1' },
    })
    expect(invitationsApi.accept).not.toHaveBeenCalled()
  })

  it('connecté avec la bonne adresse et ?accept=1 : rejoint et ouvre le groupe', async () => {
    const { router } = await mountPage('Ana@Test.local', '?accept=1')     // casse différente : même adresse

    expect(invitationsApi.accept).toHaveBeenCalledWith('tok')
    expect(router.currentRoute.value.fullPath).toBe('/b/b2/songs')
  })

  it('connecté avec une autre adresse : explique et ne rejoint pas', async () => {
    const { wrapper } = await mountPage('autre@test.local', '?accept=1')

    expect(wrapper.text()).toContain('destinée à ana@test.local')
    expect(invitationsApi.accept).not.toHaveBeenCalled()
  })
})