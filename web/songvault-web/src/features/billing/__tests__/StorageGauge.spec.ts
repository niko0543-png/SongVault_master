import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import StorageGauge from '../components/StorageGauge.vue'

const router = createRouter({
  history: createMemoryHistory(),
  routes: [{ path: '/b/:bandId/billing', name: 'billing', component: { render: () => null } },
           { path: '/:p(.*)*', component: { render: () => null } }],
})
const GiB = 1024 ** 3

function mountGauge(usedBytes: number, billingLink = false) {
  return mount(StorageGauge, {
    props: { usage: { planId: 'free', planName: 'Gratuit', usedBytes, quotaBytes: 2 * GiB }, billingLink },
    global: { plugins: [router] },
  })
}

describe('StorageGauge', () => {
  it('affiche le stockage utilisé sur le quota de la formule', () => {
    const w = mountGauge(GiB / 2)

    expect(w.text()).toContain('Stockage : 512 Mo sur 2 Go')
    expect(w.text()).toContain('(formule Gratuit)')
    expect(w.get('meter').attributes('value')).toBe('0.25')
    expect(w.find('.storage-hint').exists()).toBe(false)
  })

  it('stockage plein : prévient, et propose au propriétaire de changer de formule', async () => {
    await router.push('/b/b1/billing')
    const w = mountGauge(2 * GiB, true)

    expect(w.text()).toContain('Stockage plein : les nouveaux fichiers sont refusés.')
    expect(w.get('a').text()).toBe('Changer de formule')
  })

  it('un membre est renvoyé au propriétaire', () => {
    expect(mountGauge(1.8 * GiB).text()).toContain('Le propriétaire du groupe peut changer de formule.')
  })
})