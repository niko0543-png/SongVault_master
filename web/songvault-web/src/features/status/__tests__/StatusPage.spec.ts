import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import StatusPage from '../pages/StatusPage.vue'
import { statusApi } from '../api/statusApi'
import type { StatusReport } from '../types'

vi.mock('../api/statusApi')

function report(status: StatusReport['status'], database: StatusReport['status'] = status): StatusReport {
  return {
    status,
    checkedAt: '2026-10-17T10:00:00Z',
    components: [
      { name: 'Application', status: 'ok' },
      { name: 'Base de données', status: database },
      { name: 'Tâches de fond (e-mails, analyses, aperçus)', status },
    ],
  }
}

async function mountPage() {
  const wrapper = mount(StatusPage)
  await flushPromises()
  return wrapper
}

describe('StatusPage', () => {
  // Accolades : une fonction renvoyée par beforeEach serait appelée après le test, comme nettoyage
  beforeEach(() => {
    vi.mocked(statusApi.get).mockReset()
  })
  afterEach(() => {
    vi.useRealTimers()
  })

  it('tout fonctionne : chaque partie est opérationnelle', async () => {
    vi.mocked(statusApi.get).mockResolvedValue(report('ok'))
    const w = await mountPage()

    expect(w.get('[role="status"]').text()).toBe('Tout fonctionne')
    const rows = w.findAll('li').map((li) => li.text())
    expect(rows).toHaveLength(3)
    expect(rows.every((row) => row.endsWith('Opérationnel'))).toBe(true)
  })

  it('base en panne : la page le dit, partie par partie', async () => {
    vi.mocked(statusApi.get).mockResolvedValue(report('down'))
    const w = await mountPage()

    expect(w.get('[role="status"]').text()).toBe('SongVault est en panne')
    expect(w.findAll('li')[1]!.text()).toContain('En panne')
  })

  it('API injoignable : la page s’affiche quand même', async () => {
    vi.mocked(statusApi.get).mockResolvedValue(null)
    const w = await mountPage()

    expect(w.get('[role="status"]').text()).toBe('SongVault ne répond pas')
    expect(w.find('li').exists()).toBe(false)
  })

  it('se met à jour toute seule chaque minute', async () => {
    // Seulement setInterval : flushPromises a besoin des vrais setTimeout
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] })
    vi.mocked(statusApi.get).mockResolvedValueOnce(report('degraded')).mockResolvedValue(report('ok'))
    const w = await mountPage()
    expect(w.get('[role="status"]').text()).toBe('SongVault fonctionne, avec du retard')

    await vi.advanceTimersByTimeAsync(60_000)
    await flushPromises()

    expect(statusApi.get).toHaveBeenCalledTimes(2)
    expect(w.get('[role="status"]').text()).toBe('Tout fonctionne')
    w.unmount()
  })
})