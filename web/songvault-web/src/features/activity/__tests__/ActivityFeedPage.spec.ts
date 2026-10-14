import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { createMemoryHistory, createRouter } from 'vue-router'
import ActivityFeedPage from '../pages/ActivityFeedPage.vue'
import { activityApi } from '../api/activityApi'
import type { ActivityItem } from '../types'

vi.mock('../api/activityApi')

function item(id: number, songTitle: string): ActivityItem {
  return {
    id, type: 'SongCreated', actorId: 'u-ana', actorEmail: 'ana@test.local',
    songId: `s${id}`, songExists: true, versionId: null, entityId: null,
    songTitle, versionNumber: null, versionTitle: null, fileName: null, createdAt: '2026-10-14T10:00:00Z',
  }
}

async function mountPage() {
  const Stub = { render: () => null }
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/b/:bandId/activity', name: 'activity', component: Stub },
      { path: '/b/:bandId/songs/:songId', name: 'song-detail', component: Stub },
      { path: '/b/:bandId/songs/:songId/versions/:versionId', name: 'version-detail', component: Stub },
    ],
  })
  await router.push('/b/b1/activity')
  const wrapper = mount(ActivityFeedPage, {
    attachTo: document.body,
    global: {
      plugins: [router, createTestingPinia({
        createSpy: vi.fn,
        initialState: {
          band: { bands: [{ id: 'b1', name: 'Les Rats', role: 'Guest' }], activeId: 'b1', isLoaded: true },
          auth: { user: { id: 'u-moi', email: 'moi@test.local' } },
        },
      })],
    },
  })
  await flushPromises()
  return wrapper
}

const titles = (w: Awaited<ReturnType<typeof mountPage>>) =>
  w.findAll('[data-testid="activity-item"] a').map((a) => a.text())

describe('ActivityFeedPage', () => {
  beforeEach(() => vi.mocked(activityApi.list).mockReset())

  it('affiche les phrases, avec un lien vers chaque morceau', async () => {
    vi.mocked(activityApi.list).mockResolvedValue({ items: [item(2, 'Brume'), item(1, 'Aube')], nextCursor: null })
    const w = await mountPage()

    expect(w.findAll('[data-testid="activity-item"]')[0]!.text()).toContain('ana@test.local a créé le morceau Brume')
    expect(titles(w)).toEqual(['Brume', 'Aube'])
    expect(w.find('button').exists()).toBe(false)               // pas de « Voir plus » à la fin du fil
    w.unmount()
  })

  it('« Voir plus » demande la suite avec le curseur, l\'ajoute dessous et y place le focus', async () => {
    vi.mocked(activityApi.list)
      .mockResolvedValueOnce({ items: [item(3, 'Crépuscule'), item(2, 'Brume')], nextCursor: 'c-2' })
      .mockResolvedValueOnce({ items: [item(1, 'Aube')], nextCursor: null })
    const w = await mountPage()

    await w.get('button').trigger('click')
    await flushPromises()

    expect(activityApi.list).toHaveBeenLastCalledWith('c-2')
    expect(titles(w)).toEqual(['Crépuscule', 'Brume', 'Aube'])
    expect(document.activeElement).toBe(w.findAll('[data-testid="activity-item"]')[2]!.element)
    expect(w.find('button').exists()).toBe(false)
    w.unmount()
  })

  it('fil vide : un message', async () => {
    vi.mocked(activityApi.list).mockResolvedValue({ items: [], nextCursor: null })
    const w = await mountPage()

    expect(w.find('[data-testid="empty-state"]').exists()).toBe(true)
    w.unmount()
  })
})