import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import NotificationsSection from '../components/NotificationsSection.vue'
import { notificationsApi } from '../api/notificationsApi'
import { ApiError } from '@/shared/api/ApiError'
import type { NotificationPreference } from '../types'

vi.mock('../api/notificationsApi')

const saved: NotificationPreference[] = [
  { kind: 'NewVersion', mode: 'Immediate', isDefault: true },
  { kind: 'Comment', mode: 'Off', isDefault: false },
  { kind: 'Mention', mode: 'Immediate', isDefault: true },
]

async function mountSection() {
  const wrapper = mount(NotificationsSection)
  await flushPromises()
  return wrapper
}

describe('NotificationsSection', () => {
  beforeEach(() => {
    vi.mocked(notificationsApi.preferences).mockReset().mockResolvedValue(saved)
    vi.mocked(notificationsApi.update).mockReset().mockImplementation(async (changes) =>
      changes.map((c) => ({ ...c, isDefault: false })))
  })

  it('affiche un choix par type, avec les modes enregistrés', async () => {
    const w = await mountSection()

    expect(w.findAll('select')).toHaveLength(3)
    expect((w.get('#notif-Comment').element as HTMLSelectElement).value).toBe('Off')
    expect(w.get('label[for="notif-Mention"]').text()).toBe('Mentions')
    expect(w.get('#notif-Mention').attributes('aria-describedby')).toBe('notif-Mention-help')
  })

  it('enregistre les trois modes', async () => {
    const w = await mountSection()

    await w.get('#notif-NewVersion').setValue('Daily')
    await w.get('form').trigger('submit')
    await flushPromises()

    expect(notificationsApi.update).toHaveBeenCalledWith([
      { kind: 'NewVersion', mode: 'Daily' },
      { kind: 'Comment', mode: 'Off' },
      { kind: 'Mention', mode: 'Immediate' },
    ])
  })

  it('affiche le refus du serveur', async () => {
    vi.mocked(notificationsApi.update).mockRejectedValue(
      new ApiError(400, { title: 'Requête invalide', status: 400, detail: 'Mode inconnu.' }))
    const w = await mountSection()

    await w.get('form').trigger('submit')
    await flushPromises()

    expect(w.get('[role="alert"]').text()).toContain('Mode inconnu.')
  })
})