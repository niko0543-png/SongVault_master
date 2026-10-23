import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { createMemoryHistory, createRouter } from 'vue-router'
import SetlistDetailPage from '../pages/SetlistDetailPage.vue'
import { setlistsApi } from '../api/setlistsApi'
import type { Setlist, SetlistItem } from '../types'
import { ApiError } from '@/shared/api/ApiError'
import type { BandRole } from '@/features/bands/types'
import { songsApi } from '@/features/songs/api/songsApi'
import type { Song } from '@/features/songs/types'

// toChoices reste la vraie fonction ; seuls les appels réseau sont simulés
vi.mock('../api/setlistsApi', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../api/setlistsApi')>()),
  setlistsApi: { get: vi.fn(), setItems: vi.fn(), update: vi.fn(), remove: vi.fn() },
}))
vi.mock('@/features/songs/api/songsApi')

function item(songId: string, songTitle: string, position: number, extra: Partial<SetlistItem> = {}): SetlistItem {
  return {
    position, songId, songTitle, versionId: null, playedVersionId: `${songId}-v2`,
    versions: [{ id: `${songId}-v1`, number: 1, title: 'Démo' }, { id: `${songId}-v2`, number: 2, title: 'Live' }],
    key: null, bpm: null, notes: null, manualDurationMs: null, audioDurationMs: null, durationMs: null,
    commentBefore: null, ...extra,
  }
}

const setlist: Setlist = {
  id: 's1', name: 'Concert au Ferrailleur', date: '2026-11-28', venue: 'Nantes',
  createdAt: '2026-10-19T10:00:00Z', updatedAt: '2026-10-19T10:00:00Z',
  items: [
    item('a', 'Aube', 1, { key: 'Am', bpm: 92, audioDurationMs: 187_431, durationMs: 187_431 }),
    item('b', 'Brume', 2, { manualDurationMs: 215_000, durationMs: 215_000, notes: 'enchaîner', commentBefore: 'Présenter le groupe' }),
    item('c', 'Cendres', 3),
  ],
  totalDurationMs: 402_431, missingDurationCount: 1,
}

async function mountPage(role: BandRole = 'Member') {
  const Stub = { render: () => null }
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/b/:bandId/setlists', name: 'setlists', component: Stub },
      { path: '/b/:bandId/setlists/:setlistId', name: 'setlist-detail', component: Stub },
      { path: '/b/:bandId/songs/:songId', name: 'song-detail', component: Stub },
      { path: '/:pathMatch(.*)*', component: Stub },
    ],
  })
  await router.push('/b/b1/setlists/s1')
  const wrapper = mount(SetlistDetailPage, {
    props: { setlistId: 's1' },
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
  w.findAll('[data-testid="setlist-line"] a').map((a) => a.text())
function song(id: string, title: string, extra: Partial<Song> = {}): Song {
  return { id, title, artist: null, description: null, createdAt: '', updatedAt: '', badge: 'None', ...extra }
}
const bandSongs = [song('a', 'Aube'), song('d', 'Dune', { artist: 'Les Rats' }), song('e', 'Écume', { releaseId: 'r1' })]

const sentItems = () => vi.mocked(setlistsApi.setItems).mock.lastCall?.[1]

describe('SetlistDetailPage', () => {
  beforeEach(() => {
    vi.mocked(songsApi.list).mockReset().mockResolvedValue(
      { items: bandSongs, page: 1, pageSize: 50, totalCount: bandSongs.length, totalPages: 1 })
    vi.mocked(setlistsApi.get).mockReset().mockResolvedValue(structuredClone(setlist))
    vi.mocked(setlistsApi.setItems).mockReset().mockImplementation(async (_id, items) => ({
      ...setlist,
      items: items.map((c, i) => ({
        ...(setlist.items.find((x) => x.songId === c.songId) ?? item(c.songId, c.songId, 0)), commentBefore: c.commentBefore, position: i + 1,
      })),
    }))
  })

  it('affiche les morceaux dans l\'ordre, tonalité, BPM et la durée totale calculée par l\'API', async () => {
    const w = await mountPage()

    expect(titles(w)).toEqual(['Aube', 'Brume', 'Cendres'])
    expect(w.findAll('[data-testid="setlist-line"]')[0]!.text()).toContain('Am · 92 BPM')
    expect(w.get('[data-testid="setlist-total"]').text()).toBe('Durée totale : 6 min 42 s (+ 1 morceau sans durée)')
    expect(w.get<HTMLInputElement>('[data-testid="duration-a"]').element.placeholder).toBe('3:07')
    expect(w.get<HTMLInputElement>('[data-testid="duration-b"]').element.value).toBe('3:35')
    w.unmount()
  })

  it('réordonner au clavier enregistre la liste complète dans le nouvel ordre et l\'annonce', async () => {
    const w = await mountPage()

    await w.get('[aria-label="Monter Cendres"]').trigger('click')
    await flushPromises()

    expect(sentItems()).toEqual([
      { songId: 'a', versionId: null, notes: null, durationMs: null, commentBefore: null },
      { songId: 'c', versionId: null, notes: null, durationMs: null, commentBefore: null },
      { songId: 'b', versionId: null, notes: 'enchaîner', durationMs: 215_000, commentBefore: 'Présenter le groupe' },
    ])
    expect(titles(w)).toEqual(['Aube', 'Cendres', 'Brume'])
    expect(w.get('[aria-live="polite"]').text()).toBe('Cendres déplacé en position 2 sur 3.')
    w.unmount()
  })

  it('une durée saisie part en millisecondes ; une saisie invalide n\'enregistre rien', async () => {
    const w = await mountPage()

    await w.get('[data-testid="duration-c"]').setValue('4:05')
    await flushPromises()
    expect(sentItems()?.[2]).toEqual({ songId: 'c', versionId: null, notes: null, durationMs: 245_000, commentBefore: null })

    vi.mocked(setlistsApi.setItems).mockClear()
    await w.get('[data-testid="duration-a"]').setValue('4:99')
    await flushPromises()
    expect(setlistsApi.setItems).not.toHaveBeenCalled()
    expect(w.get('[role="alert"]').text()).toContain('Durée invalide pour « Aube »')
    w.unmount()
  })

  it('choisir une version et écrire une note enregistrent la ligne', async () => {
    const w = await mountPage()

    await w.get('[aria-label="Version de Aube"]').setValue('a-v1')
    await flushPromises()
    expect(sentItems()?.[0]).toEqual({ songId: 'a', versionId: 'a-v1', notes: null, durationMs: null, commentBefore: null })

    await w.get('[aria-label="Notes pour Cendres"]').setValue('  capo 2 ')
    await flushPromises()
    expect(sentItems()?.[2]).toEqual({ songId: 'c', versionId: null, notes: 'capo 2', durationMs: null, commentBefore: null })
    w.unmount()
  })

  it('un refus remet l\'ordre enregistré et affiche le message', async () => {
    vi.mocked(setlistsApi.setItems).mockRejectedValue(
      new ApiError(409, { title: 'Conflit', status: 409, detail: 'La ressource a été modifiée par une autre requête. Réessayez.' }))
    const w = await mountPage()

    await w.get('[aria-label="Descendre Aube"]').trigger('click')
    await flushPromises()

    expect(titles(w)).toEqual(['Aube', 'Brume', 'Cendres'])
    expect(w.get('[role="alert"]').text()).toContain('modifiée par une autre requête')
    w.unmount()
  })

  it('un Guest lit et imprime, sans rien pouvoir changer', async () => {
    const print = vi.spyOn(window, 'print').mockImplementation(() => {})
    const w = await mountPage('Guest')

    expect(titles(w)).toEqual(['Aube', 'Brume', 'Cendres'])
    expect(w.text()).toContain('Dernière : v2 · Live')
    expect(w.find('select').exists()).toBe(false)
    expect(w.find('[data-move]').exists()).toBe(false)
    expect(w.find('#setlist-search').exists()).toBe(false)
    await w.get('button').trigger('click')
    expect(print).toHaveBeenCalledOnce()
    w.unmount()
  })

  it('la feuille imprimable reprend l\'ordre, les notes et le total', async () => {
    const w = await mountPage()

    const sheet = w.get('[data-testid="print-sheet"]')
    expect(sheet.findAll('li .number').map((n) => n.text())).toEqual(['1', '2', '3'])
    expect(sheet.findAll('li .title').map((t) => t.text())).toEqual(['Aube', 'Brume — enchaîner', 'Cendres'])
    expect(sheet.findAll('li .notes').map((n) => n.text())).toEqual(['— enchaîner'])
    expect(sheet.findAll('li.comment').map((c) => c.text())).toEqual(['Présenter le groupe'])
    expect(sheet.classes()).toContain('size-xl')
    expect(sheet.get('footer').text()).toContain('Durée totale : 6 min 42 s')
    w.unmount()
  })

  it('liste les morceaux du groupe, sortis compris, et ajoute les cochés en fin de setlist dans l\'ordre coché', async () => {
    const w = await mountPage()

    expect(songsApi.list).toHaveBeenCalledWith({ search: undefined, page: 1, pageSize: 50 })
    const picker = w.get('[data-testid="song-picker"]')
    expect(picker.get<HTMLInputElement>('[data-testid="pick-a"]').element.disabled).toBe(true)   // Aube y est déjà
    expect(picker.text()).toContain('déjà dans la setlist')
    expect(picker.text()).toContain('sorti')
    expect(picker.get<HTMLButtonElement>('[data-testid="pick-add"]').element.disabled).toBe(true)

    await picker.get('[data-testid="pick-e"]').setValue(true)
    await picker.get('[data-testid="pick-d"]').setValue(true)
    expect(picker.get('[data-testid="pick-add"]').text()).toBe('Ajouter les 2 morceaux')
    await picker.get('[data-testid="pick-add"]').trigger('click')
    await flushPromises()

    expect(sentItems()?.map((c) => c.songId)).toEqual(['a', 'b', 'c', 'e', 'd'])
    expect(sentItems()?.[3]).toEqual({ songId: 'e', versionId: null, notes: null, durationMs: null, commentBefore: null })
    w.unmount()
  })

  it('le filtre relance la liste pendant la frappe', async () => {
    vi.useFakeTimers()
    try {
      const w = await mountPage()
      await w.get('#setlist-search').setValue('  du ')
      await vi.advanceTimersByTimeAsync(300)
      expect(songsApi.list).toHaveBeenLastCalledWith({ search: 'du', page: 1, pageSize: 50 })
      w.unmount()
    } finally {
      vi.useRealTimers()
    }
  })

  it('un commentaire s\'écrit entre deux morceaux et part avec la ligne qui suit', async () => {
    const w = await mountPage()

    expect(w.findAll('[data-testid="setlist-comment"]')).toHaveLength(1)
    await w.get('[aria-label="Ajouter un commentaire avant Cendres"]').trigger('click')
    await w.get('[aria-label="Commentaire avant Cendres"]').setValue('  Changer de guitare ')
    await flushPromises()
    expect(sentItems()?.[2]).toEqual({ songId: 'c', versionId: null, notes: null, durationMs: null, commentBefore: 'Changer de guitare' })

    await w.get('[aria-label="Commentaire avant Brume"]').setValue('')
    await flushPromises()
    expect(sentItems()?.[1]).toMatchObject({ songId: 'b', commentBefore: null })
    w.unmount()
  })

  it('un Guest lit les commentaires sans pouvoir en ajouter', async () => {
    const w = await mountPage('Guest')

    expect(w.get('[data-testid="setlist-comment"]').text()).toBe('Présenter le groupe')
    expect(w.find('textarea').exists()).toBe(false)
    expect(w.find('[data-testid="song-picker"]').exists()).toBe(false)
    expect(songsApi.list).not.toHaveBeenCalled()
    w.unmount()
  })
})
