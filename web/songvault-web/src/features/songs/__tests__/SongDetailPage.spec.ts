import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { createMemoryHistory, createRouter } from 'vue-router'
import SongDetailPage from '../pages/SongDetailPage.vue'
import { songsApi } from '../api/songsApi'
import { useSongsStore } from '../stores/songsStore'
import { versionsApi } from '@/features/versions/api/versionsApi'
import { ApiError } from '@/shared/api/ApiError'
import type { Song } from '../types'

vi.mock('../api/songsApi')
vi.mock('@/features/versions/api/versionsApi')

const song: Song = {
  id: 's1', title: 'Nocturne', artist: null, description: null,
  createdAt: '2026-10-12T09:00:00Z', updatedAt: '2026-10-12T09:00:00Z', badge: 'None',
}

async function mountPage() {
  const Stub = { render: () => null }
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/b/:bandId/songs/:songId', name: 'song-detail', component: Stub },
      { path: '/:pathMatch(.*)*', component: Stub },
    ],
  })
  await router.push('/b/b1/songs/s1')
  const wrapper = mount(SongDetailPage, {
    props: { songId: 's1' },
    global: {
      plugins: [router, createTestingPinia({
        createSpy: vi.fn,
        initialState: { band: { bands: [{ id: 'b1', name: 'Les Rats', role: 'Guest' }], activeId: 'b1', isLoaded: true } },
      })],
          stubs: { VersionTimeline: true, SongRightsSection: true },
    },
  })
  await flushPromises()
  return wrapper
}

describe('SongDetailPage', () => {
  beforeEach(() => {
    vi.mocked(songsApi.get).mockReset().mockResolvedValue(song)
    vi.mocked(songsApi.markSeen).mockReset().mockResolvedValue(undefined)
    vi.mocked(versionsApi.list).mockReset().mockResolvedValue([])
  })

  it("note l'ouverture du morceau et retire son badge de la liste", async () => {
    await mountPage()

    expect(songsApi.markSeen).toHaveBeenCalledWith('s1')
    expect(useSongsStore().markSeen).toHaveBeenCalledWith('s1')
  })

  it("un échec de l'ouverture n'empêche pas de lire le morceau", async () => {
    vi.mocked(songsApi.markSeen).mockRejectedValue(new ApiError(0, { title: 'Serveur injoignable', status: 0 }))

    const w = await mountPage()

    expect(w.get('h1').text()).toBe('Nocturne')
    expect(useSongsStore().markSeen).not.toHaveBeenCalled()
  })
})