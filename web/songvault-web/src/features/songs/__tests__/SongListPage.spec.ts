import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { createMemoryHistory, createRouter } from 'vue-router'
import SongListPage from '../pages/SongListPage.vue'
import { songsApi } from '../api/songsApi'
import { ApiError } from '@/shared/api/ApiError'
import type { Song } from '../types'

// Remplace le module entier : aucun appel réseau pendant les tests
vi.mock('../api/songsApi')

const song: Song = {
  id: '1', title: 'Nocturne', artist: 'Band', description: null,
  createdAt: '2026-10-12T09:00:00Z', updatedAt: '2026-10-12T09:00:00Z',
}

function mountPage() {
  const Stub = { render: () => null }
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/songs/new', name: 'song-create', component: Stub },
      { path: '/songs/:songId', name: 'song-detail', component: Stub },
      { path: '/:pathMatch(.*)*', component: Stub },
    ],
  })
  return mount(SongListPage, {
    // stubActions: false → les VRAIES actions du store s'exécutent (avec l'API simulée)
    global: { plugins: [router, createTestingPinia({ createSpy: vi.fn, stubActions: false })] },
  })
}

describe('SongListPage', () => {
  beforeEach(() => vi.mocked(songsApi.list).mockReset())

  it('affiche les morceaux', async () => {
    vi.mocked(songsApi.list).mockResolvedValue({ items: [song], page: 1, pageSize: 50, totalCount: 1, totalPages: 1 })

    const wrapper = mountPage()
    await flushPromises()                  // laisse les Promises se résoudre

    expect(wrapper.findAll('[data-testid="song-card"]')).toHaveLength(1)
    expect(wrapper.text()).toContain('Nocturne')
  })

  it("affiche l'état vide", async () => {
    vi.mocked(songsApi.list).mockResolvedValue({ items: [], page: 1, pageSize: 50, totalCount: 0, totalPages: 0 })

    const wrapper = mountPage()
    await flushPromises()

    expect(wrapper.find('[data-testid="empty-state"]').exists()).toBe(true)
  })

  it("affiche l'état d'erreur quand l'API est injoignable", async () => {
    vi.mocked(songsApi.list).mockRejectedValueOnce(
      new ApiError(0, { title: 'Serveur injoignable', status: 0 }),
    )

    const wrapper = mountPage()
    await flushPromises()

    expect(songsApi.list).toHaveBeenCalledTimes(1)
    expect(wrapper.find('[data-testid="error-state"]').text()).toContain('injoignable')
  })
})
