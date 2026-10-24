import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { createMemoryHistory, createRouter } from 'vue-router'
import ResetPasswordPage from '../pages/ResetPasswordPage.vue'
import { authApi } from '../api/authApi'
import { ApiError } from '@/shared/api/ApiError'

// Remplace le module entier : aucun appel réseau pendant les tests
vi.mock('../api/authApi')

/** Ouvre la page comme depuis le lien de l'e-mail. */
async function mountPage(query = '?email=a%40b.fr&code=abc') {
  const Stub = { render: () => null }
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/reset-password', name: 'reset-password', component: Stub },
      { path: '/forgot-password', name: 'forgot-password', component: Stub },
      { path: '/login', name: 'login', component: Stub },
    ],
  })
  await router.push(`/reset-password${query}`)
  const wrapper = mount(ResetPasswordPage, {
    global: { plugins: [router, createTestingPinia({ createSpy: vi.fn })] },
  })
  await flushPromises()
  return wrapper
}

describe('ResetPasswordPage', () => {
  beforeEach(() => {
    vi.mocked(authApi.checkResetCode).mockReset()
  })

  it("vérifie le lien dès l'ouverture et affiche le formulaire s'il est valable", async () => {
    vi.mocked(authApi.checkResetCode).mockResolvedValue(undefined)

    const wrapper = await mountPage()

    expect(authApi.checkResetCode).toHaveBeenCalledWith('a@b.fr', 'abc')
    expect(wrapper.find('form').exists()).toBe(true)
  })

  it('signale un lien déjà utilisé sans demander de mot de passe', async () => {
    vi.mocked(authApi.checkResetCode).mockRejectedValue(
      new ApiError(400, { title: 'Bad Request', status: 400, errors: { InvalidToken: ['Invalid token.'] } }))

    const wrapper = await mountPage()

    expect(wrapper.find('form').exists()).toBe(false)
    expect(wrapper.find('input[type="password"]').exists()).toBe(false)
    expect(wrapper.get('[role="alert"]').text()).toContain('déjà servi ou a expiré')
  })

  it("garde le formulaire si la vérification échoue pour une autre raison (réseau)", async () => {
    vi.mocked(authApi.checkResetCode).mockRejectedValue(new TypeError('Failed to fetch'))

    const wrapper = await mountPage()

    expect(wrapper.find('form').exists()).toBe(true)
  })

  it("n'appelle pas l'API pour un lien incomplet", async () => {
    const wrapper = await mountPage('?email=a%40b.fr')

    expect(authApi.checkResetCode).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('incomplet')
  })
})
