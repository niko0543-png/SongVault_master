import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, RouterLinkStub } from '@vue/test-utils'
import { synth } from '@coderline/alphatab'
import TrackAudioExport from '../components/TrackAudioExport.vue'
import type { ExportAudio } from '../export/renderStem'
import { stemsApi } from '@/features/stems/api/stemsApi'
import { filesApi } from '@/features/files/api/filesApi'
import type { Stem } from '@/features/stems/types'

/** #49 : création des pistes audio depuis le Guitar Pro (ADR 0049), avec un rendu et une API factices. */
vi.mock('@/features/stems/api/stemsApi', () => ({ stemsApi: { list: vi.fn(), save: vi.fn() } }))
vi.mock('@/features/files/api/filesApi', () => ({ filesApi: { upload: vi.fn() } }))
vi.mock('@/shared/composables/useToast', () => ({ useToast: () => ({ success: vi.fn(), error: vi.fn() }) }))

const stem = (fileId: string, name: string, position: number): Stem =>
  ({ fileId, name, position, originalFileName: `${name}.wav`, durationMs: null, playable: false })

/** Chaque rendu fait une seconde ; la piste dont le volume est à 1 dans `silentTracks` ne rend que du silence. */
function fakeExport(silentTracks: number[] = []) {
  return vi.fn<ExportAudio>(async (options) => {
    const audible = [...options.trackVolume].find(([, v]) => v === 1)?.[0]
    const silent = audible !== undefined && silentTracks.includes(audible)
    let done = false
    const destroy = () => {}
    return {
      render: async () => {
        if (done) return undefined
        done = true
        return Object.assign(new synth.AudioExportChunk(), {
          samples: new Float32Array(silent ? [0, 0] : [0.5, 0.5]), currentTime: 1000, endTime: 1000,
        })
      },
      destroy,
      [Symbol.dispose]: destroy,
    } as synth.IAudioExporter
  })
}

async function mountExport(exportAudio = fakeExport(), tracks = [{ name: 'Guitare' }, { name: 'Basse' }, { name: '' }]) {
  const wrapper = mount(TrackAudioExport, {
    props: { songId: 's1', versionId: 'v1', fileName: 'Riff.gp', tracks, exportAudio },
    global: { stubs: { RouterLink: RouterLinkStub } },
  })
  await flushPromises()
  return wrapper
}

const createButton = (w: Awaited<ReturnType<typeof mountExport>>) => w.get('[data-testid="track-export-create"]')

describe('TrackAudioExport (#49)', () => {
  beforeEach(() => {
    vi.mocked(stemsApi.list).mockReset().mockResolvedValue([])
    vi.mocked(stemsApi.save).mockReset().mockImplementation(async (_s, _v, input) => input.map((s, i) => stem(s.fileId, s.name, i)))
    let n = 0
    vi.mocked(filesApi.upload).mockReset().mockImplementation(async (_s, _v, file) => {
      n++
      return { id: `f${n}`, originalFileName: file.name } as Awaited<ReturnType<typeof filesApi.upload>>
    })
    vi.stubGlobal('fetch', vi.fn(async () => new Response(new Uint8Array([1, 2, 3]))))
  })
  afterEach(() => vi.unstubAllGlobals())

  it('propose chaque piste, nommée, et le clic décoché, avec la taille à prévoir', async () => {
    const wrapper = await mountExport()
    const rows = wrapper.findAll('[data-testid="track-export-row"]')
    expect(rows.map((r) => (r.findAll('input')[1]!.element as HTMLInputElement).value)).toEqual(['Guitare', 'Basse', 'Piste 3', 'Clic'])
    expect(rows.map((r) => (r.get('input[type="checkbox"]').element as HTMLInputElement).checked)).toEqual([true, true, true, false])
    expect(wrapper.get('[data-testid="track-export-size"]').text()).toContain('Durée 0:01')
    expect(createButton(wrapper).text()).toBe('Créer 3 pistes')
  })

  it('rend, envoie et ajoute les pistes après celles de la version', async () => {
    vi.mocked(stemsApi.list).mockResolvedValue([stem('old', 'Voix', 0)])
    const exportAudio = fakeExport()
    const wrapper = await mountExport(exportAudio)
    const rows = wrapper.findAll('[data-testid="track-export-row"]')
    await rows[2]!.get('input[type="checkbox"]').setValue(false)
    await rows[3]!.get('input[type="checkbox"]').setValue(true)
    await rows[0]!.findAll('input')[1]!.setValue('Guitare rythmique')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    const names = vi.mocked(filesApi.upload).mock.calls.map((c) => (c[2] as File).name)
    expect(names).toEqual(['Riff - Guitare rythmique.wav', 'Riff - Basse.wav', 'Riff - Clic.wav'])
    expect((vi.mocked(filesApi.upload).mock.calls[0]![2] as File).type).toBe('audio/wav')
    expect(stemsApi.save).toHaveBeenCalledWith('s1', 'v1', [
      { fileId: 'old', name: 'Voix' },
      { fileId: 'f1', name: 'Guitare rythmique' }, { fileId: 'f2', name: 'Basse' }, { fileId: 'f3', name: 'Clic' },
    ])
    expect(wrapper.get('[data-testid="track-export-done"]').text()).toContain('Pistes créées : Guitare rythmique, Basse, Clic.')
  })

  it('n’envoie pas une piste muette, et le dit', async () => {
    const wrapper = await mountExport(fakeExport([1]))
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(filesApi.upload).toHaveBeenCalledTimes(2)
    expect(wrapper.text()).toContain('Sans un son, donc pas créées : Basse.')
  })

  it('refuse au-delà des 8 pistes d’une version', async () => {
    vi.mocked(stemsApi.list).mockResolvedValue(Array.from({ length: 7 }, (_, i) => stem(`s${i}`, `P${i}`, i)))
    const wrapper = await mountExport()
    const boxes = wrapper.findAll('input[type="checkbox"]').map((b) => (b.element as HTMLInputElement).checked)
    expect(boxes).toEqual([true, false, false, false])                     // une seule place : la première piste
    await wrapper.findAll('input[type="checkbox"]')[1]!.setValue(true)
    expect(wrapper.text()).toContain('Il reste de la place pour 1 piste (8 au plus par version).')
    expect(createButton(wrapper).attributes('disabled')).toBeDefined()
  })

  it('garde les pistes déjà envoyées quand un envoi échoue', async () => {
    vi.mocked(filesApi.upload).mockResolvedValueOnce({ id: 'f1' } as Awaited<ReturnType<typeof filesApi.upload>>)
      .mockRejectedValueOnce(new Error('quota'))
    const wrapper = await mountExport()
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(stemsApi.save).toHaveBeenCalledWith('s1', 'v1', [{ fileId: 'f1', name: 'Guitare' }])
    expect(wrapper.find('[role="alert"]').exists()).toBe(true)
  })

  it('dit quand la banque de sons ne se charge pas', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(null, { status: 404 })))
    const wrapper = await mountExport()
    expect(wrapper.get('[role="alert"]').text()).toBe('Impossible de charger la banque de sons.')
  })
})
