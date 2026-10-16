import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { createMemoryHistory, createRouter } from 'vue-router'
import BillingPage from '../pages/BillingPage.vue'
import { billingApi } from '../api/billingApi'
import { redirectTo } from '../redirect'
import { ApiError } from '@/shared/api/ApiError'
import type { Billing, PlanCatalog } from '../types'

vi.mock('../api/billingApi')
vi.mock('../redirect')

const GiB = 1024 ** 3
const catalog: PlanCatalog = {
  onlinePayment: true,
  plans: [
    { id: 'free', name: 'Gratuit', storageQuotaBytes: 2 * GiB, maxBandsPerOwner: 1, monthlyPriceCents: 0 },
    { id: 'band', name: 'Groupe', storageQuotaBytes: 50 * GiB, maxBandsPerOwner: null, monthlyPriceCents: 600 },
    { id: 'studio', name: 'Studio', storageQuotaBytes: 250 * GiB, maxBandsPerOwner: null, monthlyPriceCents: 1500 },
  ],
}
const free: Billing = {
  usage: { planId: 'free', planName: 'Gratuit', usedBytes: GiB, quotaBytes: 2 * GiB },
  status: null, currentPeriodEnd: null, cancelAtPeriodEnd: false, isGranted: false,
  canCheckout: true, canOpenPortal: false, onlinePayment: true,
}
const paid: Billing = {
  ...free,
  usage: { planId: 'band', planName: 'Groupe', usedBytes: GiB, quotaBytes: 50 * GiB },
  status: 'Active', currentPeriodEnd: '2026-11-16T09:00:00Z', canCheckout: false, canOpenPortal: true,
}

async function mountPage(query = '') {
  const Stub = { render: () => null }
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/b/:bandId/billing', name: 'billing', component: Stub },
      { path: '/:p(.*)*', name: 'terms-of-sale', component: Stub },
    ],
  })
  await router.push(`/b/b1/billing${query}`)
  const w = mount(BillingPage, {
    global: {
      plugins: [router, createTestingPinia({
        createSpy: vi.fn,
        initialState: { band: { bands: [{ id: 'b1', name: 'Groupe', role: 'Owner' }], activeId: 'b1' } },
      })],
    },
  })
  await flushPromises()
  return w
}

describe('BillingPage', () => {
  beforeEach(() => {
    vi.mocked(billingApi.plans).mockReset().mockResolvedValue(catalog)
    vi.mocked(billingApi.billing).mockReset().mockResolvedValue(free)
    vi.mocked(billingApi.checkout).mockReset().mockResolvedValue({ url: 'https://checkout.stripe.com/c/1' })
    vi.mocked(redirectTo).mockReset()
  })

  it('groupe gratuit : propose les formules payantes et part vers Stripe', async () => {
    const w = await mountPage()

    expect(w.get('[data-testid="plan-free"]').text()).toContain('Formule actuelle')
    await w.get('[data-testid="plan-studio"] button').trigger('click')
    await flushPromises()

    expect(billingApi.checkout).toHaveBeenCalledWith('studio')
    expect(redirectTo).toHaveBeenCalledWith('https://checkout.stripe.com/c/1')
  })

  it('groupe abonné : date du prochain paiement et portail, pas de nouveau paiement', async () => {
    vi.mocked(billingApi.billing).mockResolvedValue(paid)

    const w = await mountPage()

    expect(w.text()).toContain('Abonnement : Actif.')
    expect(w.text()).toContain('Prochain paiement le')
    expect(w.text()).toContain("Gérer l'abonnement")
    expect(w.find('[data-testid="plan-studio"] button').exists()).toBe(false)
  })

  it('paiement fermé : le dit, sans bouton de paiement', async () => {
    vi.mocked(billingApi.billing).mockResolvedValue({ ...free, canCheckout: false, onlinePayment: false })

    const w = await mountPage()

    expect(w.get('[data-testid="payment-closed"]').text()).toContain("pas encore ouvert")
    expect(w.findAll('[data-testid^="plan-"] button')).toHaveLength(0)
  })

  it('Stripe injoignable : affiche le message de l\'API, reste sur la page', async () => {
    vi.mocked(billingApi.checkout).mockRejectedValue(
      new ApiError(503, { title: 'Paiement indisponible', status: 503, detail: 'Stripe ne répond pas. Réessayez.' }))

    const w = await mountPage()
    await w.get('[data-testid="plan-band"] button').trigger('click')
    await flushPromises()

    expect(w.get('[role="alert"]').text()).toBe('Stripe ne répond pas. Réessayez.')
    expect(redirectTo).not.toHaveBeenCalled()
  })

  it('retour de Stripe avant le webhook : attend, puis affiche la nouvelle formule', async () => {
    vi.useFakeTimers()
    try {
      const w = await mountPage('?checkout=success')
      expect(w.text()).toContain('Paiement reçu.')

      vi.mocked(billingApi.billing).mockResolvedValue(paid)
      await vi.advanceTimersByTimeAsync(2000)
      await flushPromises()

      expect(w.text()).toContain('Le groupe est maintenant sur la formule Groupe.')
    } finally {
      vi.useRealTimers()
    }
  })
})