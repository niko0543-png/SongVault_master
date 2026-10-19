import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { createMemoryHistory, createRouter } from 'vue-router'
import SetlistDetailPage from '../pages/SetlistDetailPage.vue'
import { setlistsApi } from '../api/setlistsApi'
import type { Setlist, SetlistItem } from '../types'
import { ApiError } from '@/shared/api/ApiError'
import type { BandRole } from '@/features/bands/types'

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
    key: null, bpm: null, notes: null, manualDurationMs: null, audioDurationMs: null, durationMs: null, ...extra,
  }
}

const setlist: Setlist = {
  id: 's1', name: 'Concert au Ferrailleur', date: '2026-11-28', venue: 'Nantes',
  createdAt: '2026-10-19T10:00:00Z', updatedAt: '2026-10-19T10:00:00Z',
  items: [
    item('a', 'Aube', 1, { key: 'Am', bpm: 92, audioDurationMs: 187_431, durationMs: 187_431 }),
    item('b', 'Brume', 2, { manualDurationMs: 215_000, durationMs: 215_000, notes: 'enchaîner' }),
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
const sentItems = () => vi.mocked(setlistsApi.setItems).mock.lastCall?.[1]

describe('SetlistDetailPage', () => {
  beforeEach(() => {
    vi.mocked(setlistsApi.get).mockReset().mockResolvedValue(structuredClone(setlist))
    vi.mocked(setlistsApi.setItems).mockReset().mockImplementation(async (_id, items) => ({
      ...setlist,
      items: items.map((c, i) => ({ ...setlist.items.find((x) => x.songId === c.songId)!, position: i + 1 })),
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
      { songId: 'a', versionId: null, notes: null, durationMs: null },
      { songId: 'c', versionId: null, notes: null, durationMs: null },
      { songId: 'b', versionId: null, notes: 'enchaîner', durationMs: 215_000 },
    ])
    expect(titles(w)).toEqual(['Aube', 'Cendres', 'Brume'])
    expect(w.get('[aria-live="polite"]').text()).toBe('Cendres déplacé en position 2 sur 3.')
    w.unmount()
  })

  it('une durée saisie part en millisecondes ; une saisie invalide n\'enregistre rien', async () => {
    const w = await mountPage()

    await w.get('[data-testid="duration-c"]').setValue('4:05')
    await flushPromises()
    expect(sentItems()?.[2]).toEqual({ songId: 'c', versionId: null, notes: null, durationMs: 245_000 })

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
    expect(sentItems()?.[0]).toEqual({ songId: 'a', versionId: 'a-v1', notes: null, durationMs: null })

    await w.get('[aria-label="Notes pour Cendres"]').setValue('  capo 2 ')
    await flushPromises()
    expect(sentItems()?.[2]).toEqual({ songId: 'c', versionId: null, notes: 'capo 2', durationMs: null })
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
    expect(sheet.classes()).toContain('size-xl')
    expect(sheet.get('footer').text()).toContain('Durée totale : 6 min 42 s')
    w.unmount()
  })
})