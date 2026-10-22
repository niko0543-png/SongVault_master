import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { nextTick } from 'vue'
import { usePlayerStore } from '@/features/player/stores/playerStore'

/** #28 : un faux alphaTab, pour vérifier ce que le lecteur lui demande sans dessiner ni jouer. */
const fake = vi.hoisted(() => {
  type Handler = (arg?: unknown) => void
  type FakeStaff = { isStringed: boolean; showStandardNotation: boolean; showTablature: boolean, bars: unknown[] }
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
    countInVolume = 0
    playbackSpeed = 1
    isLooping = false
    scoreLoaded = emitter()
    renderFinished = emitter()
    playerReady = emitter()
    playerStateChanged = emitter()
    error = emitter()
    // #29 : mesure jouée, changement de lecteur (son de la tablature ou maquette), table de temps
    playedBeatChanged = emitter()
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
    ready(names: string[]) {
      const staves = () => [{ isStringed: true, showStandardNotation: true, showTablature: true, bars: [] }]
      this.score = { tracks: names.map((name) => ({ name, staves: staves() })), masterBars: [{}, {}, {}], applyFlatSyncPoints: vi.fn() }
      this.scoreLoaded.fire(this.score)
      this.renderFinished.fire()
      this.playerReady.fire()
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
    expect(wrapper.find('[data-testid="tab-tracks"]').exists()).toBe(false)
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

  it('active le métronome et le décompte', async () => {
    const wrapper = await mountViewer()

    await wrapper.get('[data-testid="tab-metronome"]').setValue(true)
    await wrapper.get('[data-testid="tab-count-in"]').setValue(true)
    expect([api().metronomeVolume, api().countInVolume]).toEqual([1, 1])
    await wrapper.get('[data-testid="tab-metronome"]').setValue(false)
    expect(api().metronomeVolume).toBe(0)
  })
})

describe('GuitarProViewer dans la comparaison (#30)', () => {
  const beat = (n: number) => ({ n })
  const bar = (start: number) => ({ index: start / 3840, start })
  const score = {
    tracks: [{ name: 'Guitare', staves: [] }, { name: 'Basse', staves: [] }],
    masterBars: [bar(0), bar(3840), bar(7680)],
  }

  async function mountWithScore() {
    const wrapper = mount(GuitarProViewer, {
      props: { score: score as never, track: 1, label: 'riff.gp' },
      global: { plugins: [createTestingPinia({ createSpy: vi.fn, stubActions: false })] },
    })
    await flushPromises()
    api().score = score as never
    api().scoreLoaded.fire(score)
    api().renderFinished.fire()
    await nextTick()
    return wrapper
  }

  it('montre la partition donnée par la comparaison, sans rien télécharger, et change de piste', async () => {
    const wrapper = await mountWithScore()

    expect(fetchMock).not.toHaveBeenCalled()
    expect(api().renderScore).toHaveBeenCalledWith(score, [1])
    expect(wrapper.find('[data-testid="tab-tracks"]').exists()).toBe(false)            // la piste se choisit dans la comparaison
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

    expect(wrapper.get('[data-testid="tab-bar"]').text()).toBe('Mesure 2 sur 3')
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