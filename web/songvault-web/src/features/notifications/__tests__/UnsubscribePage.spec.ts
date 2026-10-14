import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import UnsubscribePage from '../pages/UnsubscribePage.vue'
import { notificationsApi } from '../api/notificationsApi'
import { ApiError } from '@/shared/api/ApiError'

vi.mock('../api/notificationsApi')

async function mountAt(url: string) {
  const Stub = { render: () => null }
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/unsubscribe', name: 'unsubscribe', component: UnsubscribePage },
      { path: '/account', name: 'account', component: Stub },
    ],
  })
  await router.push(url)
  const wrapper = mount(UnsubscribePage, { global: { plugins: [router] } })
  await flushPromises()
  return wrapper
}

describe('UnsubscribePage', () => {
  // Accolades : une fonction renvoyée par beforeEach serait appelée après le test, comme nettoyage
  beforeEach(() => {
    vi.mocked(notificationsApi.unsubscribe).mockReset()
  })

  it('ne désinscrit qu’après le clic, puis dit ce qui est coupé', async () => {
    vi.mocked(notificationsApi.unsubscribe).mockResolvedValue({ kind: 'Comment' })
    const w = await mountAt('/unsubscribe?token=abc%2Fdef')

    expect(notificationsApi.unsubscribe).not.toHaveBeenCalled()
    await w.get('button').trigger('click')
    await flushPromises()

    expect(notificationsApi.unsubscribe).toHaveBeenCalledWith('abc/def')
    expect(w.get('[role="status"]').text()).toContain('vous ne recevrez plus les e-mails « Commentaires et réponses »')
  })

  it('lien du résumé : plus aucun e-mail', async () => {
    vi.mocked(notificationsApi.unsubscribe).mockResolvedValue({ kind: null })
    const w = await mountAt('/unsubscribe?token=abc')

    await w.get('button').trigger('click')
    await flushPromises()

    expect(w.text()).toContain('vous ne recevrez plus aucun e-mail de notification')
  })

  it('sans jeton : explique quoi faire', async () => {
    const w = await mountAt('/unsubscribe')

    expect(w.find('button').exists()).toBe(false)
    expect(w.get('[role="alert"]').text()).toContain('incomplet ou a été modifié')
  })

  it('jeton refusé par l’API : même explication', async () => {
    vi.mocked(notificationsApi.unsubscribe).mockRejectedValue(new ApiError(400, { title: 'Lien invalide', status: 400 }))
    const w = await mountAt('/unsubscribe?token=abc')

    await w.get('button').trigger('click')
    await flushPromises()

    expect(w.get('[role="alert"]').text()).toContain('incomplet ou a été modifié')
  })
})