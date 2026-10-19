import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { createMemoryHistory, createRouter } from 'vue-router'
import AuditLogPage from '../pages/AuditLogPage.vue'
import { auditApi } from '../api/auditApi'
import type { AuditItem } from '../types'

vi.mock('../api/auditApi')

function item(id: number, subject: string, details: string | null = null): AuditItem {
  return {
    id, action: 'SongDeleted', actorId: 'u-ana', actorEmail: 'ana@test.local',
    subject, targetUserId: null, targetEmail: null, details, occurredAt: '2026-10-19T09:00:00Z',
  }
}

async function mountPage() {
  const Stub = { render: () => null }
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/b/:bandId/audit', name: 'audit', component: Stub }] })
  await router.push('/b/b1/audit')
  const wrapper = mount(AuditLogPage, {
    attachTo: document.body,
    global: {
      plugins: [router, createTestingPinia({
        createSpy: vi.fn,
        initialState: {
          band: { bands: [{ id: 'b1', name: 'Les Rats', role: 'Owner' }], activeId: 'b1', isLoaded: true },
          auth: { user: { id: 'u-moi', email: 'moi@test.local' } },
        },
      })],
    },
  })
  await flushPromises()
  return wrapper
}

describe('AuditLogPage', () => {
  beforeEach(() => vi.mocked(auditApi.list).mockReset())

  it('affiche une phrase par ligne, avec sa précision', async () => {
    vi.mocked(auditApi.list).mockResolvedValue({ items: [item(2, 'Brume', 'ancien nom : X'), item(1, 'Aube')], nextCursor: null })
    const w = await mountPage()

    const rows = w.findAll('[data-testid="audit-item"]')
    expect(rows.map((r) => r.find('p').text())).toEqual([
      'ana@test.local a supprimé le morceau Brume (ancien nom : X)',
      'ana@test.local a supprimé le morceau Aube',
    ])
    expect(w.text()).toContain('C’est le début du journal.')
    w.unmount()
  })

  it('« Voir plus » demande la page suivante avec le curseur', async () => {
    vi.mocked(auditApi.list)
      .mockResolvedValueOnce({ items: [item(2, 'Brume')], nextCursor: 'c-2' })
      .mockResolvedValueOnce({ items: [item(1, 'Aube')], nextCursor: null })
    const w = await mountPage()

    await w.get('button').trigger('click')
    await flushPromises()

    expect(auditApi.list).toHaveBeenLastCalledWith('c-2')
    expect(w.findAll('[data-testid="audit-item"]')).toHaveLength(2)
    w.unmount()
  })
})