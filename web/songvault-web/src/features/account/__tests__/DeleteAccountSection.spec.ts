import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { createMemoryHistory, createRouter } from 'vue-router'
import DeleteAccountSection from '../components/DeleteAccountSection.vue'
import { accountApi } from '../api/accountApi'
import { ApiError } from '@/shared/api/ApiError'
import { useConfirm } from '@/shared/composables/useConfirm'
import { useAuthStore } from '@/features/auth/stores/authStore'

vi.mock('../api/accountApi')

/** Monte la section sur /account, saisit le mot de passe et envoie le formulaire. */
async function submit() {
  const Stub = { render: () => null }
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/account', name: 'account', component: Stub },
      { path: '/login', name: 'login', component: Stub },
    ],
  })
  await router.push('/account')
  const wrapper = mount(DeleteAccountSection, {
    global: { plugins: [router, createTestingPinia({ createSpy: vi.fn })] },
  })
  await wrapper.get('input[type="password"]').setValue('Mot-de-passe1!')
  await wrapper.get('form').trigger('submit')
  await flushPromises()
  return { wrapper, router }
}

describe('DeleteAccountSection', () => {
  beforeEach(() => {
    vi.mocked(accountApi.deleteAccount).mockReset().mockResolvedValue(undefined)
  })

  it('confirmé : supprime, vide la session et mène à la connexion', async () => {
    const { router } = await submit()

    useConfirm().answer(true)
    await flushPromises()

    expect(accountApi.deleteAccount).toHaveBeenCalledWith('Mot-de-passe1!')
    expect(useAuthStore().clearSession).toHaveBeenCalled()
    expect(router.currentRoute.value.name).toBe('login')
  })

  it('annulé : ne supprime rien', async () => {
    await submit()

    useConfirm().answer(false)
    await flushPromises()

    expect(accountApi.deleteAccount).not.toHaveBeenCalled()
  })

  it('dernier Owner : affiche le message du serveur et reste sur la page', async () => {
    vi.mocked(accountApi.deleteAccount).mockRejectedValue(new ApiError(409, {
      title: 'Conflit', status: 409,
      detail: 'Vous êtes le dernier Owner de « Les Rats » : nommez d’abord un autre Owner, ou retirez les autres membres.',
    }))
    const { wrapper, router } = await submit()

    useConfirm().answer(true)
    await flushPromises()

    expect(wrapper.get('[role="alert"]').text()).toContain('« Les Rats »')
    expect(useAuthStore().clearSession).not.toHaveBeenCalled()
    expect(router.currentRoute.value.name).toBe('account')
  })
})