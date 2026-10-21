import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { nextTick } from 'vue'
import { usePlayerStore } from '@/features/player/stores/playerStore'

/** #28 : un faux alphaTab, pour vérifier ce que le lecteur lui demande sans dessiner ni jouer. */
const fake = vi.hoisted(() => {
  type Handler = (arg?: unknown) => void
  const emitter = () => {
    const handlers: Handler[] = []
    return { on: (h: Handler) => { handlers.push(h) }, fire: (arg?: unknown) => handlers.forEach((h) => h(arg)) }
  }
  class FakeApi {
    static last: FakeApi
    settings = { notation: { transpositionPitches: [] as number[] } }
    score: { tracks: { name: string }[] } | null = null
    playbackSpeed = 1
    isLooping = false
    scoreLoaded = emitter()
    renderFinished = emitter()
    playerReady = emitter()
    playerStateChanged = emitter()
    error = emitter()
    load = vi.fn((_bytes: Uint8Array) => true)
    playPause = vi.fn()
    pause = vi.fn()
    stop = vi.fn()
    destroy = vi.fn()
    render = vi.fn()
    updateSettings = vi.fn()
    renderTracks = vi.fn()
    changeTrackMute = vi.fn()
    changeTrackSolo = vi.fn()
    changeTrackTranspositionPitch = vi.fn()
    constructor(public element: HTMLElement, public options: unknown) { FakeApi.last = this }
    /** Ce que fait alphaTab après load() : partition lue, dessinée, sons chargés. */
    ready(names: string[]) {
      this.score = { tracks: names.map((name) => ({ name })) }
      this.scoreLoaded.fire(this.score)
      this.renderFinished.fire()
      this.playerReady.fire()
    }
  }
  return { FakeApi }
})
vi.mock('@coderline/alphatab', () => ({
  AlphaTabApi: fake.FakeApi,
  LayoutMode: { Page: 0 },
  PlayerMode: { EnabledSynthesizer: 2 },
  synth: { PlayerState: { Paused: 0, Playing: 1 } },
}))

import GuitarProViewer from '../components/GuitarProViewer.vue'

const api = () => fake.FakeApi.last
const fetchMock = vi.fn<typeof fetch>()

beforeEach(() => {
  fetchMock.mockReset().mockResolvedValue(new Response(new Uint8Array([0x18, 0x46, 0x49])))
  vi.stubGlobal('fetch', fetchMock)
})
afterEach(() => vi.unstubAllGlobals())

async function mountViewer(tracks = ['Guitare']) {
  const wrapper = mount(GuitarProViewer, {
    props: { src: '/api/bands/b/songs/s/versions/v/files/f/content', label: 'riff.gp5' },
    global: { plugins: [createTestingPinia({ createSpy: vi.fn, stubActions: false })] },
    attachTo: document.body,
  })
  await flushPromises()
  api().ready(tracks)
  await nextTick()
  return wrapper
}

const byLabel = (w: Awaited<ReturnType<typeof mountViewer>>, label: string) => w.get(`[aria-label="${label}"]`)

describe('GuitarProViewer (#28)', () => {
  it('lit le fichier par son adresse et le donne à alphaTab, avec la police et les sons du site', async () => {
    const wrapper = await mountViewer()

    expect(fetchMock).toHaveBeenCalledWith('/api/bands/b/songs/s/versions/v/files/f/content')
    expect([...api().load.mock.calls[0]![0]]).toEqual([0x18, 0x46, 0x49])
    expect(api().options).toMatchObject({ core: { fontDirectory: '/font/' }, player: { soundFont: '/soundfont/sonivox.sf2' } })
    expect(wrapper.get('[data-testid="tab-play"]').attributes('disabled')).toBeUndefined()
    wrapper.unmount()
    expect(api().destroy).toHaveBeenCalled()
  })

  it('annonce un fichier illisible ou introuvable', async () => {
    fetchMock.mockResolvedValue(new Response('', { status: 404 }))
    const wrapper = mount(GuitarProViewer, {
      props: { src: '/x', label: 'x.gp5' },
      global: { plugins: [createTestingPinia({ createSpy: vi.fn })] },
    })
    await flushPromises()
    expect(wrapper.get('[role="alert"]').text()).toBe('Impossible d’ouvrir ce fichier Guitar Pro.')
  })

  it('met le lecteur du bas en pause quand la tablature joue, et inversement', async () => {
    const wrapper = await mountViewer()
    const player = usePlayerStore()
    const pause = vi.spyOn(player, 'pause')

    await wrapper.get('[data-testid="tab-play"]').trigger('click')
    expect(api().playPause).toHaveBeenCalled()
    api().playerStateChanged.fire({ state: 1 })
    await nextTick()
    expect(pause).toHaveBeenCalled()
    expect(wrapper.get('[data-testid="tab-play"]').attributes('aria-label')).toBe('Mettre en pause')

    player.isPlaying = true                                    // une maquette repart dans le lecteur du bas
    await nextTick()
    expect(api().pause).toHaveBeenCalled()
  })

  it('ralentit, boucle et transpose sans toucher au fichier', async () => {
    const wrapper = await mountViewer()

    await wrapper.get('select').setValue('0.75')
    await wrapper.get('input[type="checkbox"]').setValue(true)
    expect(api().playbackSpeed).toBe(0.75)
    expect(api().isLooping).toBe(true)

    await byLabel(wrapper, 'Un demi-ton plus haut').trigger('click')
    await byLabel(wrapper, 'Un demi-ton plus haut').trigger('click')
    expect(api().changeTrackTranspositionPitch).toHaveBeenLastCalledWith(api().score!.tracks, 2)
    expect(api().settings.notation.transpositionPitches).toEqual([2])
    expect(wrapper.get('[data-testid="tab-transpose"]').text()).toBe('+2 demi-tons')

    await byLabel(wrapper, 'Un demi-ton plus bas').trigger('click')
    await byLabel(wrapper, 'Un demi-ton plus bas').trigger('click')
    expect(wrapper.get('[data-testid="tab-transpose"]').text()).toBe('Tonalité du fichier')
  })

  it('choisit les pistes affichées, et coupe ou isole une piste à l’écoute', async () => {
    const wrapper = await mountViewer(['Guitare', 'Basse'])
    const [guitar, bass] = api().score!.tracks

    await byLabel(wrapper, 'Afficher Basse').trigger('change')
    expect(api().renderTracks).toHaveBeenLastCalledWith([guitar, bass])
    await byLabel(wrapper, 'Afficher Guitare').trigger('change')
    expect(api().renderTracks).toHaveBeenLastCalledWith([bass])
    await byLabel(wrapper, 'Afficher Basse').trigger('change')       // la dernière piste reste affichée
    expect(api().renderTracks).toHaveBeenCalledTimes(2)

    await byLabel(wrapper, 'Couper Basse').trigger('click')
    expect(api().changeTrackMute).toHaveBeenLastCalledWith([bass], true)
    expect(byLabel(wrapper, 'Couper Basse').attributes('aria-pressed')).toBe('true')
    await byLabel(wrapper, 'Écouter Guitare seule').trigger('click')
    expect(api().changeTrackSolo).toHaveBeenLastCalledWith([guitar], true)
  })

  it('ne montre pas la liste des pistes pour un fichier à une seule piste', async () => {
    const wrapper = await mountViewer(['Guitare'])
    expect(wrapper.find('fieldset').exists()).toBe(false)
  })
})