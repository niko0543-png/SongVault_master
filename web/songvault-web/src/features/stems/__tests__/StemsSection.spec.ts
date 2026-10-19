import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import StemsSection from '../components/StemsSection.vue'
import { stemsApi } from '../api/stemsApi'
import type { Stem } from '../types'
import type { SongFile } from '@/features/files/types'
import { FakeAudioContext } from './fakeAudio'

vi.mock('../api/stemsApi', () => ({ stemsApi: { list: vi.fn(), save: vi.fn() } }))

const file = (id: string, name: string, fileType: SongFile['fileType'] = 'Audio'): SongFile => ({
  id, originalFileName: name, contentType: 'audio/wav', sizeBytes: 10, fileType,
  uploadedAt: '2026-10-19T16:00:00Z', preview: 'Ready', scan: 'Clean',
})
const files = [file('f1', 'voix.wav'), file('f2', 'guitare.wav'), file('f3', 'batterie.wav'), file('f4', 'paroles.pdf', 'Document')]
const stem = (fileId: string, name: string, position: number, over: Partial<Stem> = {}): Stem => ({
  fileId, name, position, originalFileName: `${name.toLowerCase()}.wav`, durationMs: 180_000, playable: true, ...over,
})

async function mountSection(readonly = false) {
  const wrapper = mount(StemsSection, {
    props: { songId: 's1', versionId: 'v1', files, readonly },
    global: {
      plugins: [createTestingPinia({
        createSpy: vi.fn,
        initialState: { band: { bands: [{ id: 'b1', name: 'Les Rats', role: readonly ? 'Guest' : 'Member' }], activeId: 'b1', isLoaded: true } },
      })],
    },
  })
  await flushPromises()
  return wrapper
}

const buttonByText = (w: Awaited<ReturnType<typeof mountSection>>, text: string) =>
  w.findAll('button').find((b) => b.text().startsWith(text))

describe('StemsSection', () => {
  let contexts: FakeAudioContext[]

  beforeEach(() => {
    vi.mocked(stemsApi.list).mockReset().mockResolvedValue([])
    vi.mocked(stemsApi.save).mockReset().mockImplementation(async (_s, _v, input) =>
      input.map((s, i) => stem(s.fileId, s.name, i)))
    contexts = []
    vi.stubGlobal('AudioContext', vi.fn(function () {
      const ctx = new FakeAudioContext()
      contexts.push(ctx)
      return ctx
    }))
    vi.stubGlobal('fetch', vi.fn(async () => new Response(new Uint8Array([180]))))
  })

  afterEach(() => vi.unstubAllGlobals())

  it('enregistre les pistes choisies, nommées et dans l’ordre', async () => {
    const wrapper = await mountSection()
    await buttonByText(wrapper, 'Choisir les pistes')!.trigger('click')

    await wrapper.get('select[aria-label="Fichier audio à ajouter"]').setValue('f3')
    await buttonByText(wrapper, 'Ajouter une piste')!.trigger('click')
    await buttonByText(wrapper, 'Ajouter une piste')!.trigger('click')       // sans choix : le premier restant
    const names = wrapper.findAll('[data-testid="stem-row"] input')
    await names[0]!.setValue('Batterie')
    await names[1]!.setValue('Voix')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(stemsApi.save).toHaveBeenCalledWith('s1', 'v1', [{ fileId: 'f3', name: 'Batterie' }, { fileId: 'f1', name: 'Voix' }])
    expect(wrapper.text()).toContain('Ouvrir le mélangeur (2 pistes)')
  })

  it('ne propose que les fichiers audio et refuse une piste sans nom', async () => {
    const wrapper = await mountSection()
    await buttonByText(wrapper, 'Choisir les pistes')!.trigger('click')

    const options = wrapper.findAll('select[aria-label="Fichier audio à ajouter"] option').map((o) => o.text())
    expect(options).not.toContain('paroles.pdf')
    await buttonByText(wrapper, 'Ajouter une piste')!.trigger('click')
    await wrapper.get('[data-testid="stem-row"] input').setValue('  ')

    expect(wrapper.text()).toContain('Chaque piste a besoin d’un nom.')
    expect(buttonByText(wrapper, 'Enregistrer les pistes')!.attributes('disabled')).toBeDefined()
  })

  it('ouvre le mélangeur avec les seules pistes jouables', async () => {
    vi.mocked(stemsApi.list).mockResolvedValue([
      stem('f1', 'Voix', 0), stem('f2', 'Guitare', 1, { playable: false, durationMs: null }), stem('f3', 'Batterie', 2),
    ])
    const wrapper = await mountSection()
    expect(wrapper.get('[data-testid="stems-waiting"]').text()).toContain('Guitare')

    await buttonByText(wrapper, 'Ouvrir le mélangeur (2 pistes)')!.trigger('click')
    await flushPromises()

    expect(fetch).toHaveBeenCalledTimes(2)
    expect(vi.mocked(fetch).mock.calls.map((c) => String(c[0]))).toEqual([
      '/api/bands/b1/songs/s1/versions/v1/files/f1/listen',
      '/api/bands/b1/songs/s1/versions/v1/files/f3/listen',
    ])
    expect(wrapper.findAll('[data-testid="mixer-track"]').map((t) => t.get('.name').text())).toEqual(['Voix', 'Batterie'])
  })

  it('refuse d’ouvrir le mélangeur au-delà de la mémoire de l’appareil', async () => {
    vi.mocked(stemsApi.list).mockResolvedValue(
      Array.from({ length: 8 }, (_, i) => stem(`f${i}`, `Piste ${i + 1}`, i, { durationMs: 300_000 })))
    const wrapper = await mountSection()

    expect(wrapper.get('[data-testid="stems-too-long"]').text()).toContain('40:00 de pistes au total')
    expect(buttonByText(wrapper, 'Ouvrir le mélangeur')!.attributes('disabled')).toBeDefined()
    expect(AudioContext).not.toHaveBeenCalled()
  })

  it('montre les pistes à un Guest sans lui proposer de les modifier', async () => {
    vi.mocked(stemsApi.list).mockResolvedValue([stem('f1', 'Voix', 0)])
    const wrapper = await mountSection(true)

    expect(buttonByText(wrapper, 'Modifier les pistes')).toBeUndefined()
    expect(buttonByText(wrapper, 'Ouvrir le mélangeur (1 piste)')).toBeDefined()
  })

  it('ferme le contexte audio en quittant la page', async () => {
    vi.mocked(stemsApi.list).mockResolvedValue([stem('f1', 'Voix', 0)])
    const wrapper = await mountSection()
    await buttonByText(wrapper, 'Ouvrir le mélangeur')!.trigger('click')
    await flushPromises()

    wrapper.unmount()

    expect(contexts[0]!.close).toHaveBeenCalled()
  })
})