import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import CreateVersionForm from '../components/CreateVersionForm.vue'
import type { CreateSongVersionInput, SongVersionSummary } from '../types'

/** ADR 0046 : à la création, on reprend des éléments d'une version existante. */
afterEach(() => vi.unstubAllGlobals())

const summary = (id: string, number: number): SongVersionSummary =>
  ({ id, number, title: `Titre ${number}`, status: 'Demo', createdAt: '2026-10-22T10:00:00Z' })
const file = (id: string, sizeBytes: number, scan = 'Clean') =>
  ({ id, originalFileName: `${id}.mp3`, contentType: 'audio/mpeg', sizeBytes, fileType: 'Audio', uploadedAt: '2026-10-22T10:00:00Z', preview: null, scan })

function version(id: string, extra: Record<string, unknown> = {}) {
  return { id, songId: 's1', number: 2, title: 'Démo', status: 'Demo', notes: 'Tempo à revoir', lyrics: '[C]Refrain', bpm: 92,
           key: 'F#m', createdAt: '2026-10-22T10:00:00Z', updatedAt: '2026-10-22T10:00:00Z',
           files: [file('mix', 1024), file('voix', 2048), file('bloque', 10, 'Infected')], ...extra }
}

function mountForm(versions: SongVersionSummary[]) {
  const fetchMock = vi.fn<(url: string) => Promise<Response>>(async (url) =>
    new Response(JSON.stringify(version(url.split('/').pop()!)), { headers: { 'Content-Type': 'application/json' } }))
  vi.stubGlobal('fetch', fetchMock)
  const wrapper = mount(CreateVersionForm, {
    props: { songId: 's1', versions },
    global: { plugins: [createTestingPinia({ createSpy: vi.fn, initialState: { band: { activeId: 'b1' } } })] },
  })
  return { wrapper, fetchMock }
}

async function submit(wrapper: ReturnType<typeof mountForm>['wrapper']) {
  await wrapper.find('#version-title').setValue('Studio')
  await wrapper.find('form').trigger('submit')
  return wrapper.emitted('submit')![0]![0] as CreateSongVersionInput
}

describe('CreateVersionForm, reprise d’une version (ADR 0046)', () => {
  it('sans version existante, pas de bloc de reprise ni d’appel', () => {
    const { wrapper, fetchMock } = mountForm([])
    expect(wrapper.find('[data-testid="carry-over"]').exists()).toBe(false)
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('part de la dernière version et reprend par défaut tout sauf les notes, fichiers lisibles compris', async () => {
    const { wrapper, fetchMock } = mountForm([summary('v1', 1), summary('v2', 2)])
    await flushPromises()

    expect(String(fetchMock.mock.calls[0]![0])).toContain('/versions/v2')
    expect((wrapper.find('#version-carry-from').element as HTMLSelectElement).value).toBe('v2')
    expect(wrapper.text()).toContain('2 fichiers à copier')
    const boxes = wrapper.findAll('input[type="checkbox"]')
    const blocked = boxes[boxes.length - 1]!
    expect(blocked.attributes('disabled')).toBeDefined()

    const input = await submit(wrapper)
    expect(input).toMatchObject({ title: 'Studio', notes: null, lyrics: '[C]Refrain', bpm: 92, key: 'F#m',
                                  copyFromVersionId: 'v2', copyFileIds: ['mix', 'voix'] })
  })

  it('décocher retire les éléments ; cocher les notes les copie dans le champ Notes', async () => {
    const { wrapper } = mountForm([summary('v1', 1)])
    await flushPromises()
    const boxes = wrapper.findAll('input[type="checkbox"]')
    await boxes[0]!.setValue(false)              // paroles
    await boxes[2]!.setValue(false)              // tonalité
    await boxes[3]!.setValue(true)               // notes
    expect((wrapper.find('#version-notes').element as HTMLTextAreaElement).value).toBe('Tempo à revoir')
    await wrapper.findAll('button.link')[1]!.trigger('click')   // tout décocher

    const input = await submit(wrapper)
    expect(input).toMatchObject({ notes: 'Tempo à revoir', lyrics: null, bpm: 92, key: null,
                                  copyFromVersionId: null, copyFileIds: [] })
  })
})
