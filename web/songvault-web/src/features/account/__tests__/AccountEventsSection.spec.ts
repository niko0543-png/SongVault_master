import { describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import AccountEventsSection from '../components/AccountEventsSection.vue'
import { accountApi } from '../api/accountApi'

vi.mock('../api/accountApi')

describe('AccountEventsSection', () => {
  it('traduit chaque événement et signale les tentatives douteuses', async () => {
    vi.mocked(accountApi.accountEvents).mockResolvedValue([
      { id: 2, type: 'SignInFailed', occurredAt: '2026-10-19T09:05:00Z', network: '203.0.113.0/24', device: 'Chrome sur Android' },
      { id: 1, type: 'SignedInWithCode', occurredAt: '2026-10-19T09:00:00Z', network: null, device: 'Firefox sur Windows' },
    ])
    const w = mount(AccountEventsSection)
    await flushPromises()

    const rows = w.findAll('[data-testid="account-event"]')
    expect(rows[0]!.text()).toContain('Mot de passe incorrect')
    expect(rows[0]!.text()).toContain('Chrome sur Android · réseau 203.0.113.0/24')
    expect(rows[0]!.classes()).toContain('warning')
    expect(rows[1]!.text()).toContain('Connexion avec code')
    expect(rows[1]!.classes()).not.toContain('warning')
  })
})