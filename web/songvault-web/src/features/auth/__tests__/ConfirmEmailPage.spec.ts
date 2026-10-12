import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { createMemoryHistory, createRouter, RouterLink } from 'vue-router'
import ConfirmEmailPage from '../pages/ConfirmEmailPage.vue'
import { authApi } from '../api/authApi'
import { ApiError } from '@/shared/api/ApiError'

// Remplace le module entier : aucun appel réseau pendant les tests
vi.mock('../api/authApi')

/** Ouvre la page comme depuis le lien de l'e-mail, sans être connecté. */
async function mountPage() {
  const Stub = { render: () => null }
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/confirm-email', name: 'confirm-email', component: Stub },
      { path: '/login', name: 'login', component: Stub },
      { path: '/:pathMatch(.*)*', component: Stub },
    ],
  })
  await router.push('/confirm-email?userId=u1&code=abc')
  const wrapper = mount(ConfirmEmailPage, {
    global: { plugins: [router, createTestingPinia({ createSpy: vi.fn })] },
  })
  await flushPromises()
  return wrapper
}

describe('ConfirmEmailPage', () => {
  beforeEach(() => {
    vi.mocked(authApi.confirmEmail).mockReset()
    localStorage.clear()
  })

  it("confirme avec les paramètres du lien puis mène à la connexion, avec l'invitation en attente", async () => {
    vi.mocked(authApi.confirmEmail).mockResolvedValue(undefined)
    localStorage.setItem('songvault.afterConfirm', '/invite/tok?accept=1')

    const wrapper = await mountPage()

    expect(String(vi.mocked(authApi.confirmEmail).mock.calls[0]?.[0])).toBe('userId=u1&code=abc')
    expect(wrapper.text()).toContain('Votre adresse est confirmée.')
    const [login] = wrapper.findAllComponents(RouterLink)
    expect(login?.props('to')).toEqual({ name: 'login', query: { redirect: '/invite/tok?accept=1' } })
    expect(localStorage.getItem('songvault.afterConfirm')).toBeNull()        // ne sert qu'une fois
  })

  it('explique quoi faire quand le lien est invalide ou expiré', async () => {
    vi.mocked(authApi.confirmEmail).mockRejectedValue(new ApiError(401, { title: 'Unauthorized', status: 401 }))

    const wrapper = await mountPage()

    expect(wrapper.text()).toContain('invalide ou a expiré')
  })
})