import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { createMemoryHistory, createRouter } from 'vue-router'
import ReleaseDetailPage from '../pages/ReleaseDetailPage.vue'
import { releasesApi } from '../api/releasesApi'
import type { Release, ReleaseTrack } from '../types'
import { ApiError } from '@/shared/api/ApiError'
import type { BandRole } from '@/features/bands/types'

// toChoices reste la vraie fonction ; seuls les appels réseau sont simulés
vi.mock('../api/releasesApi', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../api/releasesApi')>()),
  releasesApi: {
    get: vi.fn(), setTracks: vi.fn(), update: vi.fn(), remove: vi.fn(),
    uploadCover: vi.fn(), removeCover: vi.fn(), coverUrl: () => '/cover',
  },
}))

vi.mock('../api/releaseExportsApi')                // #24 : section de l'archive, testée à part
vi.mock('@/features/songs/api/songsApi')
vi.mock('@/features/versions/api/versionsApi')

function track(songId: string, songTitle: string, trackNumber: number): ReleaseTrack {
  return {
    trackNumber, songId, songTitle, versionId: `${songId}-v1`,
    versions: [
      { id: `${songId}-v1`, number: 1, title: 'Démo', status: 'Demo' },
      { id: `${songId}-v2`, number: 2, title: 'Master', status: 'Final' },
    ],
    isReleasedHere: false,
  }
}

const release: Release = {
  id: 'r1', title: 'Nuits blanches', type: 'EP', releaseDate: '2026-11-20', hasCover: false,
  createdAt: '2026-10-14T10:00:00Z', updatedAt: '2026-10-14T10:00:00Z',
  tracks: [track('a', 'Aube', 1), track('b', 'Brume', 2), track('c', 'Crépuscule', 3)],
  label: null, upc: null, streamingLinks: [],
}

async function mountPage(role: BandRole = 'Member') {
  const Stub = { render: () => null }
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/b/:bandId/releases', name: 'releases', component: Stub },
      { path: '/b/:bandId/releases/:releaseId', name: 'release-detail', component: Stub },
      { path: '/b/:bandId/songs/:songId', name: 'song-detail', component: Stub },
      { path: '/:pathMatch(.*)*', component: Stub },
    ],
  })
  await router.push('/b/b1/releases/r1')
  const wrapper = mount(ReleaseDetailPage, {
    props: { releaseId: 'r1' },
    attachTo: document.body,
    global: {
      plugins: [router, createTestingPinia({
        createSpy: vi.fn,
        stubActions: false,                // bands.can() doit vraiment lire le rôle
        initialState: { band: { bands: [{ id: 'b1', name: 'Les Rats', role }], activeId: 'b1', isLoaded: true } },
      })],
    },
  })
  await flushPromises()
  return wrapper
}

const titles = (w: Awaited<ReturnType<typeof mountPage>>) =>
  w.findAll('[data-testid="release-track"] a').map((a) => a.text())
const sentTracks = () => vi.mocked(releasesApi.setTracks).mock.lastCall?.[1]

describe('ReleaseDetailPage', () => {
  beforeEach(() => {
    vi.mocked(releasesApi.get).mockReset().mockResolvedValue(structuredClone(release))
    vi.mocked(releasesApi.setTracks).mockReset().mockImplementation(async (_id, tracks) => ({
      ...release,
      tracks: tracks.map((c, i) => ({ ...release.tracks.find((t) => t.songId === c.songId)!, versionId: c.versionId, trackNumber: i + 1 })),
    }))
  })

  it('affiche les pistes dans l\'ordre avec la version choisie', async () => {
    const w = await mountPage()

    expect(titles(w)).toEqual(['Aube', 'Brume', 'Crépuscule'])
    expect(w.text()).toContain('v1 · Démo (Démo)')
    expect(w.text()).toContain('EP · 20 novembre 2026')
    w.unmount()
  })

  it('#39 : pas de saisie ici, ni titre, ni version, ni ajout de morceau, ni infos de sortie', async () => {
    const w = await mountPage()

    expect(w.find('[aria-label^="Version de"]').exists()).toBe(false)
    expect(w.find('#track-search').exists()).toBe(false)
    expect(w.findAll('button').map((b) => b.text())).not.toContain('Modifier')
    expect(w.find('[data-move]').exists()).toBe(true)                      // l'ordre des pistes reste réglable
    w.unmount()
  })

  it('un Guest voit les pistes et leurs versions, sans rien pouvoir changer', async () => {
    const w = await mountPage('Guest')

    expect(titles(w)).toEqual(['Aube', 'Brume', 'Crépuscule'])
    expect(w.text()).toContain('v1 · Démo (Démo)')
    expect(w.find('select').exists()).toBe(false)
    expect(w.find('[data-move]').exists()).toBe(false)
    expect(w.find('#track-search').exists()).toBe(false)
    w.unmount()
  })

  it('réordonner enregistre la liste complète dans le nouvel ordre', async () => {
    const w = await mountPage()

    await w.get('[aria-label="Descendre Aube"]').trigger('click')
    await flushPromises()

    expect(sentTracks()).toEqual([
      { songId: 'b', versionId: 'b-v1' }, { songId: 'a', versionId: 'a-v1' }, { songId: 'c', versionId: 'c-v1' },
    ])
    expect(titles(w)).toEqual(['Brume', 'Aube', 'Crépuscule'])
    w.unmount()
  })

  it('un refus remet l\'ordre enregistré et affiche le message', async () => {
    vi.mocked(releasesApi.setTracks).mockRejectedValue(
      new ApiError(409, { title: 'Conflit', status: 409, detail: 'La ressource a été modifiée par une autre requête. Réessayez.' }))
    const w = await mountPage()

    await w.get('[aria-label="Monter Crépuscule"]').trigger('click')
    await flushPromises()

    expect(titles(w)).toEqual(['Aube', 'Brume', 'Crépuscule'])
    expect(w.get('[role="alert"]').text()).toContain('modifiée par une autre requête')
    w.unmount()
  })

})