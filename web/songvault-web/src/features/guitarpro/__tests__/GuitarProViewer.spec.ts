import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { nextTick } from 'vue'
import { usePlayerStore } from '@/features/player/stores/playerStore'

/** #28 : un faux alphaTab, pour vérifier ce que le lecteur lui demande sans dessiner ni jouer. */
const fake = vi.hoisted(() => {
  type Handler = (arg?: unknown) => void
  type FakeStaff = { isStringed: boolean; showStandardNotation: boolean; showTablature: boolean; bars: unknown[] }
  const emitter = () => {
    const handlers: Handler[] = []
    return { on: (h: Handler) => { handlers.push(h) }, fire: (arg?: unknown) => handlers.forEach((h) => h(arg)) }
  }
  class FakeApi {
    static last: FakeApi
    settings = { notation: { transpositionPitches: [] as number[] }, player: { playerMode: 2 }, display: { scale: 1, layoutMode: 0 } }
    score: { tracks: { name: string; staves: FakeStaff[] }[]; masterBars: unknown[]; applyFlatSyncPoints: (p: unknown[]) => void } | null = null
    // #31 : métronome et décompte
    metronomeVolume = 0
    masterVolume = 1
    countInVolume = 0
    playbackSpeed = 1
    isLooping = false
    scoreLoaded = emitter()
    renderFinished = emitter()
    postRenderFinished = emitter()
    beatMouseDown = emitter()
    playerReady = emitter()
    playerStateChanged = emitter()
    error = emitter()
    // #29 : mesure jouée, changement de lecteur (son de la tablature ou maquette), table de temps
    playedBeatChanged = emitter()
    // #33 : téléchargement des sons, boucle
    soundFontLoad = emitter()
    // #34 : temps écoulé, lecture depuis une note
    playerPositionChanged = emitter()
    play = vi.fn()
    playbackRangeChanged = emitter()
    applyPlaybackRangeFromHighlight = vi.fn()
    clearPlaybackRangeHighlight = vi.fn()
    loadMidiForScore = vi.fn()
    player = { output: {} as { handler?: unknown; updatePosition?: (ms: number) => void } }
    tickCache: { masterBars: { masterBar: { index: number } }[] } | null = null
    playbackRange: unknown = null
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
    changeTrackVolume = vi.fn()
    changeTrackTranspositionPitch = vi.fn()
    // #30 : partition donnée toute prête, et placement sur une mesure
    renderScore = vi.fn()
    highlightPlaybackRange = vi.fn()
    tickPosition = 0
    tracks: unknown[] = []
    boundsLookup: { findMasterBarByIndex: (i: number) => { realBounds: { y: number } } | null } | null = null
    constructor(public element: HTMLElement, public options: unknown) { FakeApi.last = this }
    /** Ce que fait alphaTab après load() : partition lue, dessinée, sons chargés. */
    ready(names: string[], withSound = true) {
      const staves = () => [{ isStringed: true, showStandardNotation: true, showTablature: true, bars: [] }]
      this.score = { tracks: names.map((name) => ({ name, staves: staves() })), masterBars: [{}, {}, {}], applyFlatSyncPoints: vi.fn() }
      this.scoreLoaded.fire(this.score)
      this.renderFinished.fire()
      if (withSound) this.playerReady.fire()
    }
  }
  return { FakeApi }
})
vi.mock('@coderline/alphatab', () => ({
  AlphaTabApi: fake.FakeApi,
  LayoutMode: { Page: 0, Horizontal: 1, Parchment: 2 },
  PlayerMode: { EnabledSynthesizer: 2, EnabledExternalMedia: 4 },
  synth: { PlayerState: { Paused: 0, Playing: 1 } },
}))

import GuitarProViewer from '../components/GuitarProViewer.vue'

const api = () => fake.FakeApi.last
const fetchMock = vi.fn<typeof fetch>()

beforeEach(() => {
  localStorage.clear()                                           // #33 : les réglages gardés d'un test à l'autre
  fetchMock.mockReset().mockResolvedValue(new Response(new Uint8Array([0x18, 0x46, 0x49])))
  vi.stubGlobal('fetch', fetchMock)
})
afterEach(() => vi.unstubAllGlobals())

async function mountViewer(tracks = ['Guitare'], fill = false) {
  const wrapper = mount(GuitarProViewer, {
    props: { src: '/api/bands/b/songs/s/versions/v/files/f/content', label: 'riff.gp5', fill },
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
    expect(api().options).toMatchObject({ core: { fontDirectory: '/font/' }, player: { soundFont: '/sounds/generaluser-gs-2.0.3.sf2' } })
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

    for (let i = 0; i < 5; i++) await wrapper.get('[data-testid="tab-slower"]').trigger('click')
    await wrapper.get('[data-testid="tab-loop"]').trigger('click')
    expect(api().playbackSpeed).toBe(0.75)
    expect(api().isLooping).toBe(true)

    await byLabel(wrapper, 'Un demi-ton plus haut').trigger('click')
    await byLabel(wrapper, 'Un demi-ton plus haut').trigger('click')
    expect(api().changeTrackTranspositionPitch).toHaveBeenLastCalledWith(api().score!.tracks, 2)
    expect(api().settings.notation.transpositionPitches).toEqual([2])
    expect(wrapper.get('[data-testid="tab-transpose"]').text()).toBe('+2')
    expect(wrapper.get('[data-testid="tab-transpose"]').attributes('aria-label')).toBe('+2 demi-tons')

    await byLabel(wrapper, 'Un demi-ton plus bas').trigger('click')
    await byLabel(wrapper, 'Un demi-ton plus bas').trigger('click')
    expect(wrapper.get('[data-testid="tab-transpose"]').attributes('aria-label')).toBe('Tonalité du fichier')
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

  it('#34 : montre le panneau dès une piste, sans muet ni solo', async () => {
    const wrapper = await mountViewer(['Guitare'])
    expect(wrapper.find('[data-testid="tab-tracks"]').exists()).toBe(true)
    expect(wrapper.find('[aria-label="Couper Guitare"]').exists()).toBe(false)
  })
})

describe('GuitarProViewer façon Guitar Pro (#31)', () => {
  it('dessine avec la mise en page du fichier sur grand écran, les couleurs de Guitar Pro, sur une page blanche', async () => {
    const wrapper = await mountViewer()

    expect(api().options).toMatchObject({ display: { layoutMode: 2, resources: { staffLineColor: '#6b6b6b' } } })
    expect(wrapper.find('.desk [data-testid="tab-score"]').exists()).toBe(true)
  })

  it('zoome, change les portées montrées et revient à celles du fichier, sans toucher au fichier', async () => {
    const wrapper = await mountViewer()
    const staff = api().score!.tracks[0]!.staves[0]!

    await wrapper.get('[data-testid="tab-zoom"]').setValue('1.25')
    expect(api().settings.display.scale).toBe(1.25)
    expect(api().updateSettings).toHaveBeenCalled()

    await wrapper.get('[data-testid="tab-staves"]').setValue('tab')
    expect(staff).toMatchObject({ showStandardNotation: false, showTablature: true })
    await wrapper.get('[data-testid="tab-staves"]').setValue('score')
    expect(staff).toMatchObject({ showStandardNotation: true, showTablature: false })
    await wrapper.get('[data-testid="tab-staves"]').setValue('file')
    expect(staff).toMatchObject({ showStandardNotation: true, showTablature: true })
    expect(api().render).toHaveBeenCalledTimes(4)
    expect(fetchMock).toHaveBeenCalledTimes(1)                      // rien n'est renvoyé à l'API
  })

  it('règle le volume d’une piste et place la partition depuis la vue d’ensemble des mesures', async () => {
    const wrapper = await mountViewer(['Guitare', 'Basse'])
    const [, bass] = api().score!.tracks
    api().score!.masterBars = [0, 3840, 7680].map((start, index) => ({ index, start }))
    api().tracks = [{ staves: [{ bars: [{}, { voices: [{ beats: [{}] }] }, {}] }] }]

    await byLabel(wrapper, 'Volume de Basse').setValue('0.5')
    expect(api().changeTrackVolume).toHaveBeenLastCalledWith([bass], 0.5)
    await wrapper.findAll('[data-testid="tab-overview"] .lane')[1]!.findAll('.cell')[1]!.trigger('click')
    expect(api().tickPosition).toBe(3840)                           // la lecture partira de la mesure 2
  })

  it('règle le volume général de toutes les pistes, le garde sur l’appareil et le reprend à l’ouverture', async () => {
    const wrapper = await mountViewer(['Guitare', 'Basse'])

    await byLabel(wrapper, 'Volume général').setValue('0.5')
    expect(api().masterVolume).toBeCloseTo(0.8)                      // × 1,6 : GeneralUser GS sort moins fort (ADR 0049)
    expect(api().changeTrackVolume).not.toHaveBeenCalled()             // le mix des pistes ne bouge pas
    wrapper.unmount()

    const again = await mountViewer(['Guitare', 'Basse'])
    expect(api().masterVolume).toBeCloseTo(0.8)                      // redonné quand les sons sont prêts
    expect(byLabel(again, 'Volume général').attributes('aria-valuetext')).toBe('50 %')
  })

  it('active le métronome et le décompte', async () => {
    const wrapper = await mountViewer()

    await wrapper.get('[data-testid="tab-metronome"]').trigger('click')
    await wrapper.get('[data-testid="tab-count-in"]').trigger('click')
    expect(wrapper.get('[data-testid="tab-metronome"]').attributes('aria-pressed')).toBe('true')
    expect([api().metronomeVolume, api().countInVolume]).toEqual([1, 1])
    await wrapper.get('[data-testid="tab-metronome"]').trigger('click')
    expect(api().metronomeVolume).toBe(0)
  })
})

describe('GuitarProViewer sur la page du lecteur (#32)', () => {
  const press = (key: string, target: EventTarget = document.body) =>
    target.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }))

  it('en plein cadre, alphaTab fait défiler la partition et non la page', async () => {
    const wrapper = await mountViewer(['Guitare'], true)
    expect(wrapper.classes()).toContain('fill')
    expect((api().options as { player: { scrollElement?: unknown } }).player.scrollElement)
      .toBe(wrapper.get('[data-testid="tab-desk"]').element)
    wrapper.unmount()

    const inline = await mountViewer()
    expect((api().options as { player: { scrollElement?: unknown } }).player.scrollElement).toBeUndefined()
    inline.unmount()
  })

  it('se pilote au clavier : Espace, L, Échap et les flèches, avant le lecteur du bas', async () => {
    const wrapper = await mountViewer(['Guitare'], true)
    api().score!.masterBars = [0, 3840, 7680].map((start, index) => ({ index, start }))
    const bottomPlayer = vi.fn()
    window.addEventListener('keydown', bottomPlayer)               // comme AudioPlayer : sans capture

    press(' ')
    expect(api().playPause).toHaveBeenCalledTimes(1)
    expect(bottomPlayer).not.toHaveBeenCalled()                     // Espace ne lance pas aussi la maquette du bas
    press('L')
    await nextTick()
    expect(api().isLooping).toBe(true)
    press('Escape')
    expect(api().stop).toHaveBeenCalled()

    press('ArrowRight')
    await nextTick()
    expect(api().tickPosition).toBe(3840)
    expect(wrapper.get('[data-testid="tab-bar"]').attributes('aria-label')).toBe('Mesure 2 sur 3')
    expect(wrapper.get('[data-testid="tab-bar"]').text()).toBe('2/3')
    press('ArrowLeft')
    expect(api().tickPosition).toBe(0)
    expect(api().highlightPlaybackRange).not.toHaveBeenCalled()      // se déplacer ne sélectionne pas

    press(' ', wrapper.get('[data-testid="tab-tempo-value"]').element)   // un bouton a le focus : la touche est à lui
    expect(api().playPause).toHaveBeenCalledTimes(1)
    window.removeEventListener('keydown', bottomPlayer)
    wrapper.unmount()
  })

  it('ailleurs (comparaison, banc d’essai), le clavier reste au lecteur du bas', async () => {
    const wrapper = await mountViewer()
    press(' ')
    expect(api().playPause).not.toHaveBeenCalled()
    wrapper.unmount()
  })

  it('règle la vitesse par pas de 5 %, de 25 à 150 %, et revient à 100 % d’un clic sur la valeur', async () => {
    const wrapper = await mountViewer(['Guitare'], true)
    const value = wrapper.get('[data-testid="tab-tempo-value"]')

    for (let i = 0; i < 8; i++) await wrapper.get('[data-testid="tab-slower"]').trigger('click')
    expect(api().playbackSpeed).toBe(0.6)
    expect(value.text()).toBe('60 %')
    for (let i = 0; i < 20; i++) await wrapper.get('[data-testid="tab-slower"]').trigger('click')
    expect(value.text()).toBe('25 %')
    expect(wrapper.get('[data-testid="tab-slower"]').attributes('disabled')).toBeDefined()
    await value.trigger('click')
    expect(api().playbackSpeed).toBe(1)
    for (let i = 0; i < 20; i++) await wrapper.get('[data-testid="tab-faster"]').trigger('click')
    expect(value.text()).toBe('150 %')
    wrapper.unmount()
  })
})

describe('GuitarProViewer, confort de travail (#33)', () => {
  /** Une tablature de la version (fichier f ou autre), sans calage enregistré. */
  async function mountFile(fileId = 'f', tracks = ['Guitare', 'Basse'], withSound = true) {
    fetchMock.mockImplementation(async (input) =>
      String(input).endsWith('/content') ? new Response(new Uint8Array([0x18, 0x46, 0x49])) : new Response(null, { status: 204 }))
    const wrapper = mount(GuitarProViewer, {
      props: { src: '/content', label: 'riff.gp', fill: true, sync: { songId: 's', versionId: 'v', fileId, audios: [], canEdit: true } },
      global: { plugins: [createTestingPinia({ createSpy: vi.fn, stubActions: false, initialState: { band: { activeId: 'b1' } } })] },
      attachTo: document.body,
    })
    await flushPromises()
    api().ready(tracks, withSound)
    await flushPromises()
    return wrapper
  }
  const bars = (count: number) => Array.from({ length: count }, (_, i) => ({ voices: [{ beats: [{ bar: i, n: 0 }, { bar: i, n: 1 }] }] }))

  it('boucle des mesures données par leur numéro, par « Début ici » et « Fin ici », ou glissées sur la partition', async () => {
    const wrapper = await mountFile()
    api().score!.masterBars = [0, 3840, 7680, 11520].map((start, index) => ({ index, start }))
    api().tracks = [{ staves: [{ bars: bars(4) }] }]
    api().scoreLoaded.fire(api().score)                                 // quatre mesures
    const beatsOf = (bar: number) => (api().tracks[0] as { staves: { bars: { voices: { beats: unknown[] }[] }[] }[] }).staves[0]!.bars[bar]!.voices[0]!.beats
    const value = (id: string) => (wrapper.get(`[data-testid="${id}"]`).element as HTMLInputElement).value

    api().playedBeatChanged.fire({ voice: { bar: { index: 2 } } })     // la mesure 3 joue
    await nextTick()
    await wrapper.get('[data-testid="tab-loop-start"]').trigger('click')
    expect(api().highlightPlaybackRange).toHaveBeenLastCalledWith(beatsOf(2)[0], beatsOf(2)[1])
    expect(api().applyPlaybackRangeFromHighlight).toHaveBeenCalled()
    expect([value('tab-loop-from'), value('tab-loop-to')]).toEqual(['3', '3'])

    api().playedBeatChanged.fire({ voice: { bar: { index: 3 } } })
    await nextTick()
    await wrapper.get('[data-testid="tab-loop-end"]').trigger('click')
    expect(api().highlightPlaybackRange).toHaveBeenLastCalledWith(beatsOf(2)[0], beatsOf(3)[1])

    await wrapper.get('[data-testid="tab-loop-from"]').setValue('1')
    expect(api().highlightPlaybackRange).toHaveBeenLastCalledWith(beatsOf(0)[0], beatsOf(3)[1])

    api().playbackRangeChanged.fire({ playbackRange: { startTick: 3840, endTick: 7700 } })   // glissé sur la partition
    await nextTick()
    expect([value('tab-loop-from'), value('tab-loop-to')]).toEqual(['2', '3'])

    await wrapper.get('[data-testid="tab-loop-clear"]').trigger('click')
    expect(api().clearPlaybackRangeHighlight).toHaveBeenCalled()
    expect(api().playbackRange).toBeNull()
    expect(wrapper.find('[data-testid="tab-loop-clear"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('garde sur l’appareil zoom, portées et panneau pour toutes les tablatures, tempo et mix par fichier', async () => {
    const first = await mountFile('f')
    await first.get('[data-testid="tab-zoom"]').setValue('1.25')
    await first.get('[data-testid="tab-staves"]').setValue('tab')
    for (let i = 0; i < 8; i++) await first.get('[data-testid="tab-slower"]').trigger('click')
    await byLabel(first, 'Couper Basse').trigger('click')
    await byLabel(first, 'Volume de Guitare').setValue('1.5')
    await first.get('[data-testid="tab-tracks-toggle"]').trigger('click')
    first.unmount()

    const again = await mountFile('f')
    const [guitar, bass] = api().score!.tracks
    expect(api().options).toMatchObject({ display: { scale: 1.25 } })
    expect((again.get('[data-testid="tab-staves"]').element as HTMLSelectElement).value).toBe('tab')
    expect(api().score!.tracks[0]!.staves[0]).toMatchObject({ showStandardNotation: false, showTablature: true })
    expect(api().render).not.toHaveBeenCalled()                       // posées avant le premier rendu
    expect(again.get('[data-testid="tab-tempo-value"]').text()).toBe('60 %')
    expect(api().playbackSpeed).toBe(0.6)
    expect(api().changeTrackMute).toHaveBeenCalledWith([bass], true)
    expect(api().changeTrackVolume).toHaveBeenCalledWith([guitar], 1.5)
    expect(again.get('[data-testid="tab-tracks-toggle"]').attributes('aria-expanded')).toBe('false')
    again.unmount()

    const other = await mountFile('g')                                   // un autre fichier : son tempo et son mix à lui
    expect(other.get('[data-testid="tab-tempo-value"]').text()).toBe('100 %')
    expect(api().changeTrackMute).not.toHaveBeenCalled()
    expect(api().options).toMatchObject({ display: { scale: 1.25 } })
    other.unmount()
  })

  it('ignore des réglages abîmés ou un mix d’un autre nombre de pistes', async () => {
    localStorage.setItem('songvault.tab.view', '{"zoom":7,"staffView":"x"')
    localStorage.setItem('songvault.tab.file.f', JSON.stringify({ speed: 9, mute: [true], solo: [false], volume: [1] }))
    const wrapper = await mountFile('f')
    expect(api().options).toMatchObject({ display: { scale: 1 } })
    expect(wrapper.get('[data-testid="tab-tempo-value"]').text()).toBe('100 %')
    expect(api().changeTrackMute).not.toHaveBeenCalled()
    wrapper.unmount()
  })

  it('montre l’avancement du téléchargement des sons', async () => {
    const wrapper = await mountFile('f', ['Guitare'], false)
    api().soundFontLoad.fire({ loaded: 450, total: 1000 })
    await nextTick()
    expect(wrapper.get('[data-testid="tab-sound-loading"]').text()).toBe('Chargement des sons… 45 %')
    api().playerReady.fire()
    await nextTick()
    expect(wrapper.find('[data-testid="tab-sound-loading"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('replie les réglages derrière un bouton (affiché sur téléphone)', async () => {
    const wrapper = await mountFile()
    const toggle = wrapper.get('[data-testid="tab-settings-toggle"]')
    expect(toggle.attributes('aria-expanded')).toBe('false')
    expect(wrapper.get('[data-testid="tab-settings"]').classes()).not.toContain('open')
    await toggle.trigger('click')
    expect(toggle.attributes('aria-expanded')).toBe('true')
    expect(wrapper.get('[data-testid="tab-settings"]').classes()).toContain('open')
    wrapper.unmount()
  })
})

describe('GuitarProViewer, finitions (#34)', () => {
  it('affiche le temps écoulé et la durée, et des boutons à icône nommés', async () => {
    const wrapper = await mountViewer()
    expect(wrapper.get('[data-testid="tab-time"]').text()).toBe('00:00 / 00:00')
    api().playerPositionChanged.fire({ currentTime: 42_500, endTime: 151_000 })
    await nextTick()
    expect(wrapper.get('[data-testid="tab-time"]').text()).toBe('00:42 / 02:31')
    expect(wrapper.get('[data-testid="tab-stop"]').attributes('aria-label')).toBe('Revenir au début')
    expect(wrapper.find('[data-testid="tab-stop"] svg').exists()).toBe(true)
    expect(wrapper.find('[data-testid="tab-play"] svg').exists()).toBe(true)
    wrapper.unmount()
  })

  it('un double-clic sur la partition lance la lecture depuis la note, pas pendant la lecture', async () => {
    const wrapper = await mountViewer()
    await wrapper.get('[data-testid="tab-score"]').trigger('dblclick')
    expect(api().play).toHaveBeenCalledTimes(1)
    api().playerStateChanged.fire({ state: 1 })
    await nextTick()
    await wrapper.get('[data-testid="tab-score"]').trigger('dblclick')
    expect(api().play).toHaveBeenCalledTimes(1)
    wrapper.unmount()
  })
})

describe('GuitarProViewer, barre façon Guitar Pro (#35)', () => {
  it('l’écran montre la piste affichée, la mesure, le chiffrage et le tempo du fichier à la mesure jouée', async () => {
    const wrapper = await mountViewer(['Guitare', 'Basse'])
    const score = api().score as unknown as { tempo: number; tracks: { color?: { rgba: string } }[]; masterBars: unknown[] }
    score.tempo = 120
    score.tracks[1]!.color = { rgba: 'rgb(0, 0, 255)' }
    score.masterBars = [
      { index: 0, start: 0, timeSignatureNumerator: 4, timeSignatureDenominator: 4, tempoAutomations: [] },
      { index: 1, start: 3840, timeSignatureNumerator: 4, timeSignatureDenominator: 4, tempoAutomations: [{ value: 170 }] },
      { index: 2, start: 7680, timeSignatureNumerator: 7, timeSignatureDenominator: 8, tempoAutomations: [] },
    ]
    api().scoreLoaded.fire({ ...api().score })                          // partition relue : l'écran se recalcule
    await nextTick()
    expect(wrapper.get('[data-testid="tab-track-name"]').text()).toBe('1. Guitare')
    expect(wrapper.get('[data-testid="tab-bpm"]').text()).toBe('♩ = 120')
    expect(wrapper.get('[data-testid="tab-signature"]').text()).toBe('4/4')

    api().playedBeatChanged.fire({ voice: { bar: { index: 2 } } })
    await nextTick()
    expect(wrapper.get('[data-testid="tab-bpm"]').text()).toBe('♩ = 170')      // le changement de tempo de la mesure 2 tient
    expect(wrapper.get('[data-testid="tab-signature"]').text()).toBe('7/8')

    await byLabel(wrapper, 'Afficher Guitare').trigger('change')               // seule la basse reste affichée
    await byLabel(wrapper, 'Afficher Basse').trigger('change')
    await byLabel(wrapper, 'Afficher Guitare').trigger('change')
    expect(wrapper.get('[data-testid="tab-track-name"]').text()).toBe('2. Basse')
    expect(wrapper.get('.stripe').attributes('style')).toContain('rgb(0, 0, 255)')
    wrapper.unmount()
  })

  it('va à la mesure précédente, suivante ou à la dernière sans sélectionner', async () => {
    const wrapper = await mountViewer()
    api().score!.masterBars = [0, 3840, 7680].map((start, index) => ({ index, start }))
    await wrapper.get('[data-testid="tab-next"]').trigger('click')
    expect(api().tickPosition).toBe(3840)
    await wrapper.get('[data-testid="tab-last"]').trigger('click')
    expect(api().tickPosition).toBe(7680)
    await wrapper.get('[data-testid="tab-prev"]').trigger('click')
    expect(api().tickPosition).toBe(3840)
    expect(api().highlightPlaybackRange).not.toHaveBeenCalled()
    wrapper.unmount()
  })

  it('sur la page du lecteur, les réglages restent repliés derrière ⋮ et l’appareil s’en souvient', async () => {
    const first = await mountViewer(['Guitare'], true)
    expect(first.get('[data-testid="tab-settings"]').classes()).not.toContain('open')
    await first.get('[data-testid="tab-settings-toggle"]').trigger('click')
    first.unmount()
    const again = await mountViewer(['Guitare'], true)
    expect(again.get('[data-testid="tab-settings-toggle"]').attributes('aria-expanded')).toBe('true')
    expect(again.get('[data-testid="tab-settings"]').classes()).toContain('open')
    again.unmount()
  })
})

describe('GuitarProViewer dans la comparaison (#30)', () => {
  const beat = (n: number) => ({ n })
  const bar = (start: number) => ({ index: start / 3840, start })
  const score = {
    tracks: [{ name: 'Guitare', staves: [] }, { name: 'Basse', staves: [] }],
    masterBars: [bar(0), bar(3840), bar(7680)],
  }

  async function mountWithScore(start: { startTracks?: number[]; startBar?: number } = {}) {
    const wrapper = mount(GuitarProViewer, {
      props: { score: score as never, track: 1, label: 'riff.gp', ...start },
      global: { plugins: [createTestingPinia({ createSpy: vi.fn, stubActions: false })] },
    })
    await flushPromises()
    api().score = score as never
    api().scoreLoaded.fire(score)
    api().renderFinished.fire()
    api().postRenderFinished.fire()
    await nextTick()
    return wrapper
  }

  it('montre la partition donnée par la comparaison, sans rien télécharger, et change de piste', async () => {
    const wrapper = await mountWithScore()

    expect(fetchMock).not.toHaveBeenCalled()
    expect(api().renderScore).toHaveBeenCalledWith(score, [1])
    expect(wrapper.find('[data-testid="tab-tracks"]').exists()).toBe(true)    // #37 : le panneau des pistes reste dans la comparaison
    await wrapper.setProps({ track: 0 })
    expect(api().renderTracks).toHaveBeenLastCalledWith([score.tracks[0]])
  })

  it('se place sur une mesure : sélection, départ de la lecture et défilement', async () => {
    const wrapper = await mountWithScore()
    const beats = [beat(1), beat(2), beat(3)]
    api().tracks = [{ staves: [{ bars: [{}, { voices: [{ beats }] }, {}] }] }]
    api().boundsLookup = { findMasterBarByIndex: (i) => (i === 1 ? { realBounds: { y: 400 } } : null) }
    const scrollTo = vi.spyOn(window, 'scrollTo').mockImplementation(() => {})

    ;(wrapper.vm as unknown as { goToBar: (i: number) => void }).goToBar(1)

    expect(api().highlightPlaybackRange).toHaveBeenCalledWith(beats[0], beats[2])
    expect(api().tickPosition).toBe(3840)
    expect(scrollTo).toHaveBeenCalledWith(expect.objectContaining({ behavior: 'smooth' }))
    scrollTo.mockRestore()
  })

  it('#37 : reprend les pistes et la mesure données, une fois la partition placée et le son prêt', async () => {
    const wrapper = await mountWithScore({ startTracks: [0, 1], startBar: 2 })

    expect(api().renderScore).toHaveBeenCalledWith(score, [0, 1])
    expect(api().tickPosition).toBe(0)                                  // avant le son, alphaTab remettrait le curseur au début
    api().playerReady.fire()
    await nextTick()
    expect(api().tickPosition).toBe(7680)                               // placé sur la mesure 3
    expect((wrapper.vm as unknown as { position: () => unknown }).position()).toEqual({ tracks: [0, 1], bar: 2 })
    api().tickPosition = 0                                              // un nouveau rendu (zoom…) ne le replace pas
    api().postRenderFinished.fire()
    await nextTick()
    expect(api().tickPosition).toBe(0)
  })

  it('#37 : un clic sur une note change la mesure en cours', async () => {
    const wrapper = await mountWithScore()
    api().beatMouseDown.fire({ voice: { bar: { index: 1 } } })
    await nextTick()
    expect(wrapper.get('[data-testid="tab-bar"]').attributes('aria-label')).toBe('Mesure 2 sur 3')
    expect((wrapper.vm as unknown as { position: () => { bar: number } }).position().bar).toBe(1)
  })
})

describe('GuitarProViewer calé sur une maquette (#29)', () => {
  const syncUrl = '/api/bands/b1/songs/s/versions/v/files/f/sync'
  const points = [
    { barIndex: 0, barOccurence: 0, barPosition: 0, millisecondOffset: 1000 },
    { barIndex: 1, barOccurence: 0, barPosition: 0, millisecondOffset: 2500 },
  ]
  const json = (body: unknown) => new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } })

  /** La tablature, puis l'API du calage : rien d'enregistré, ou la maquette a1 et ses points. */
  function routes(saved: unknown) {
    fetchMock.mockImplementation(async (input, init) => {
      const url = String(input)
      if (url.endsWith('/content')) return new Response(new Uint8Array([0x18, 0x46, 0x49]))
      if (url === syncUrl && init?.method === 'PUT') return json({ audioFileId: 'a1', audioFileName: 'maquette.wav', ...JSON.parse(String(init.body)) })
      if (url === syncUrl && init?.method === 'DELETE') return new Response(null, { status: 204 })
      if (url === syncUrl) return saved ? json(saved) : new Response(null, { status: 204 })
      return new Response('', { status: 404 })
    })
  }

  async function mountSynced(saved: unknown, canEdit = true) {
    routes(saved)
    const wrapper = mount(GuitarProViewer, {
      props: {
        src: '/api/bands/b1/songs/s/versions/v/files/f/content', label: 'riff.gp5',
        sync: { songId: 's', versionId: 'v', fileId: 'f', audios: [{ id: 'a1', name: 'maquette.wav' }], canEdit },
      },
      global: { plugins: [createTestingPinia({ createSpy: vi.fn, stubActions: false, initialState: { band: { activeId: 'b1' } } })] },
      attachTo: document.body,
    })
    await flushPromises()
    api().ready(['Guitare'])
    await flushPromises()
    return wrapper
  }

  it('sans calage, la tablature garde son propre son ; un Member peut la caler, un Guest non', async () => {
    const wrapper = await mountSynced(null)

    expect(api().settings.player.playerMode).toBe(2)
    expect(wrapper.find('[data-testid="tab-source"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="sync-start"]').text()).toBe('Caler sur une maquette')
    wrapper.unmount()

    const guest = await mountSynced(null, false)
    expect(guest.find('[data-testid="sync-start"]').exists()).toBe(false)
  })

  it('avec un calage, suit la maquette dès l’ouverture, et revient au son de la tablature à la demande', async () => {
    const wrapper = await mountSynced({ audioFileId: 'a1', audioFileName: 'maquette.wav', points })

    expect(wrapper.get('[data-testid="tab-audio"]').attributes('src')).toBe('/api/bands/b1/songs/s/versions/v/files/a1/listen')
    expect(api().settings.player.playerMode).toBe(4)
    expect(api().score!.applyFlatSyncPoints).toHaveBeenLastCalledWith(points)
    expect(api().loadMidiForScore).toHaveBeenCalled()
    expect(api().player.output.handler).toBeDefined()                  // l'élément <audio> commande alphaTab
    expect(byLabel(wrapper, 'Un demi-ton plus haut').attributes('disabled')).toBeDefined()

    // Lecture possible quand le nouveau lecteur est prêt ET que la maquette connaît sa durée
    api().playerReady.fire()
    await nextTick()
    expect(wrapper.get('[data-testid="tab-play"]').attributes('disabled')).toBeDefined()
    await wrapper.get('[data-testid="tab-audio"]').trigger('loadedmetadata')
    expect(wrapper.get('[data-testid="tab-play"]').attributes('disabled')).toBeUndefined()

    // Le volume général règle aussi la maquette suivie
    await byLabel(wrapper, 'Volume général').setValue('0.25')
    expect((wrapper.get('[data-testid="tab-audio"]').element as HTMLAudioElement).volume).toBe(0.25)

    await wrapper.get('[data-testid="tab-source"]').setValue('synth')
    await flushPromises()
    expect(api().settings.player.playerMode).toBe(2)
    expect(api().score!.applyFlatSyncPoints).toHaveBeenLastCalledWith([])
    expect(api().player.output.handler).toBeUndefined()
  })

  it('montre la mesure jouée', async () => {
    const wrapper = await mountSynced(null)

    api().playedBeatChanged.fire({ voice: { bar: { index: 1 } } })
    await nextTick()

    expect(wrapper.get('[data-testid="tab-bar"]').attributes('aria-label')).toBe('Mesure 2 sur 3')
    expect(wrapper.get('[data-testid="tab-bar"]').text()).toBe('2/3')
  })

  it('enregistre un calage au tapé, dans l’ordre de lecture (reprises comprises), puis suit la maquette', async () => {
    const wrapper = await mountSynced(null)
    const index = (i: number) => ({ masterBar: { index: i } })
    api().tickCache = { masterBars: [index(0), index(1), index(0), index(1), index(2)] }   // mesures 1-2 reprises
    api().playerReady.fire()
    await nextTick()

    await wrapper.get('[data-testid="sync-start"]').trigger('click')
    const recorder = wrapper.findComponent({ name: 'TabSyncRecorder' })
    expect(recorder.props('bars')).toBe(5)
    recorder.vm.$emit('save', 'a1', [500, 1500, 2500, 3500])
    await flushPromises()

    const put = fetchMock.mock.calls.find(([, init]) => init?.method === 'PUT')!
    expect(JSON.parse(String(put[1]!.body))).toEqual({
      audioFileId: 'a1',
      points: [
        { barIndex: 0, barOccurence: 0, barPosition: 0, millisecondOffset: 500 },
        { barIndex: 1, barOccurence: 0, barPosition: 0, millisecondOffset: 1500 },
        { barIndex: 0, barOccurence: 1, barPosition: 0, millisecondOffset: 2500 },
        { barIndex: 1, barOccurence: 1, barPosition: 0, millisecondOffset: 3500 },
      ],
    })
    expect(wrapper.findComponent({ name: 'TabSyncRecorder' }).exists()).toBe(false)
    expect(api().settings.player.playerMode).toBe(4)
    expect((wrapper.get('[data-testid="tab-source"]').element as HTMLSelectElement).value).toBe('audio')
  })
})