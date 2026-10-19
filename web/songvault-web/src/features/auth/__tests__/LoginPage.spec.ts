import { describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { createMemoryHistory, createRouter } from 'vue-router'
import LoginPage from '../pages/LoginPage.vue'
import { useAuthStore } from '../stores/authStore'
import { ApiError } from '@/shared/api/ApiError'

const unauthorized = (detail: string) => new ApiError(401, { title: 'Unauthorized', status: 401, detail })

async function mountPage() {
  const Stub = { render: () => null }
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/login', name: 'login', component: Stub },
      { path: '/', name: 'home', component: Stub },
      { path: '/register', name: 'register', component: Stub },
      { path: '/forgot-password', name: 'forgot-password', component: Stub },
    ],
  })
  await router.push('/login')
  const wrapper = mount(LoginPage, {
    attachTo: document.body,
    global: { plugins: [router, createTestingPinia({ createSpy: vi.fn })] },
  })
  await wrapper.get('#login-email').setValue('moi@test.local')
  await wrapper.get('#login-password').setValue('Mot-de-passe1!')
  return { wrapper, router, auth: useAuthStore() }
}

describe('LoginPage (#19 : double authentification)', () => {
  it('bon mot de passe, code demandé : passe à l’étape du code puis l’envoie avec le mot de passe', async () => {
    const { wrapper, router, auth } = await mountPage()
    vi.mocked(auth.login).mockRejectedValueOnce(unauthorized('RequiresTwoFactor')).mockResolvedValueOnce(undefined)

    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.find('[role="alert"]').exists()).toBe(false)        // pas une erreur
    expect(document.activeElement?.id).toBe('login-code')

    await wrapper.get('#login-code').setValue('123 456')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(auth.login).toHaveBeenLastCalledWith({ email: 'moi@test.local', password: 'Mot-de-passe1!', twoFactorCode: '123456' })
    expect(router.currentRoute.value.name).toBe('home')
    wrapper.unmount()
  })

  it('un code de secours part dans son propre champ ; un refus dit « Code incorrect »', async () => {
    const { wrapper, auth } = await mountPage()
    vi.mocked(auth.login).mockRejectedValueOnce(unauthorized('RequiresTwoFactor')).mockRejectedValueOnce(unauthorized('Failed'))
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    await wrapper.findAll('button').find((b) => b.text() === 'Utiliser un code de secours')!.trigger('click')
    await wrapper.get('#login-code').setValue('aaaa1-bbbb1')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(auth.login).toHaveBeenLastCalledWith({ email: 'moi@test.local', password: 'Mot-de-passe1!', twoFactorRecoveryCode: 'aaaa1-bbbb1' })
    expect(wrapper.get('[role="alert"]').text()).toContain('Code incorrect')
    wrapper.unmount()
  })
})