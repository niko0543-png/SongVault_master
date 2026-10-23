import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { exporter, importer, model, Settings } from '@coderline/alphatab'

/**
 * #30 : la comparaison lit deux vrais fichiers Guitar Pro (écrits ici par alphaTab à partir d'alphaTex) ;
 * seul le lecteur est remplacé : il note la partition reçue et les mesures demandées.
 */
const viewer = vi.hoisted(() => ({ goToBar: vi.fn(), score: null as unknown }))
vi.mock('../components/GuitarProViewer.vue', () => ({
  default: defineComponent({
    name: 'GuitarProViewer',
    props: { score: Object, track: Number, label: String, fill: Boolean },
    setup(props, { expose }) {
      expose({ goToBar: viewer.goToBar })
      return () => {
        viewer.score = props.score
        return h('div', { 'data-testid': 'viewer' }, props.label)
      }
    },
  }),
}))

vi.mock('@/features/files/api/filesApi', () => ({
  filesApi: { contentUrl: (_song: string, versionId: string, fileId: string) => `/content/${versionId}/${fileId}` },
}))

import GuitarProCompare from '../components/GuitarProCompare.vue'

const RIFF = ['0.6.4 2.6.4 3.6.4 0.6.4', '0.5.4 2.5.4 3.5.4 0.5.4', '0.4.4 2.4.4 3.4.4 0.4.4']

function gp(bars: string[], tuning = 'E4 B3 G3 D3 A2 E2'): Uint8Array {
  const score = importer.ScoreLoader.loadAlphaTex(`\\tempo 90 \\track "Guitare" \\staff {tabs} \\tuning (${tuning}) ${bars.join(' | ')}`, new Settings())
  return new exporter.Gp7Exporter().export(score, new Settings())
}

const fetchMock = vi.fn<typeof fetch>()
function serve(files: Record<string, Uint8Array | null>) {
  fetchMock.mockImplementation(async (url) => {
    const bytes = files[String(url).split('/').pop()!]
    return bytes ? new Response(bytes as Uint8Array<ArrayBuffer>) : new Response('', { status: 404 })
  })
}

beforeEach(() => {
  viewer.goToBar.mockReset()
  vi.stubGlobal('fetch', fetchMock)
})

async function mountCompare() {
  const wrapper = mount(GuitarProCompare, {
    props: { songId: 's', a: { versionId: 'va', fileId: 'fa' }, b: { versionId: 'vb', fileId: 'fb' }, label: 'riff.gp' },
  })
  await flushPromises()
  await vi.waitFor(() => expect(wrapper.find('[aria-busy="false"]').exists()).toBe(true))
  return wrapper
}

describe('GuitarProCompare (#30)', () => {
  it('liste les différences de B par rapport à A, et un clic place la partition sur la mesure', async () => {
    const after = ['12.1.1', ...RIFF]
    after[3] = '0.4.4 2.4.4 5.4.4 0.4.4'
    serve({ fa: gp(RIFF), fb: gp(after) })
    const wrapper = await mountCompare()

    expect(fetchMock).toHaveBeenCalledWith('/content/va/fa')
    expect(fetchMock).toHaveBeenCalledWith('/content/vb/fb')
    const items = wrapper.findAll('[data-testid="gp-changes"] li')
    expect(items.map((li) => li.text())).toEqual(['Mesure 1 : ajoutée', 'Mesure 4 : 1 note modifiée, 1 note retirée'])
    expect(wrapper.text()).toContain('2 différences sur cette piste :')
    await items[1]!.get('button').trigger('click')
    expect(viewer.goToBar).toHaveBeenCalledWith(3)
  })

  it('colore dans la partition de B les notes ajoutées en vert et modifiées en orange', async () => {
    const after = ['12.1.1', ...RIFF]
    after[3] = '0.4.4 2.4.4 5.4.4 0.4.4'
    serve({ fa: gp(RIFF), fb: gp(after) })
    await mountCompare()

    const score = viewer.score as model.Score
    const noteAt = (bar: number, beat: number) => score.tracks[0]!.staves[0]!.bars[bar]!.voices[0]!.beats[beat]!.notes[0]!
    const fret = (bar: number, beat: number) => noteAt(bar, beat).style?.colors.get(model.NoteSubElement.GuitarTabFretNumber)?.rgba
    expect(fret(0, 0)).toBe('#1A7F37')
    expect(fret(3, 2)).toBe('#B35900')
    expect(fret(3, 1)).toBeUndefined()
    expect(fret(1, 0)).toBeUndefined()
  })

  it('annonce un changement d’accordage en une ligne, sans différence de notes', async () => {
    serve({ fa: gp(RIFF), fb: gp(RIFF, 'E4 B3 G3 D3 A2 D2') })
    const wrapper = await mountCompare()

    expect(wrapper.get('[data-testid="gp-global"] li').text()).toBe('Guitare : accordage E A D G B E → D A D G B E (les cordes et les cases jouées sont comparées telles quelles)')
    expect(wrapper.text()).toContain('Aucune différence de notes sur cette piste.')
    expect(wrapper.find('[data-testid="gp-changes"]').exists()).toBe(false)
  })

  it('annonce un fichier illisible', async () => {
    serve({ fa: gp(RIFF), fb: null })
    const wrapper = await mountCompare()
    expect(wrapper.get('[role="alert"]').text()).toBe('Impossible de comparer ces fichiers Guitar Pro.')
  })
})