import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, RouterLinkStub } from '@vue/test-utils'
import { exporter, importer, Settings } from '@coderline/alphatab'
import type { SongFile } from '@/features/files/types'
import type { SongVersion } from '@/features/versions/types'

/** #36 : le résumé à côté du fichier de B, dans l'écran A/B, sur deux vrais fichiers Guitar Pro écrits par alphaTab. */
vi.mock('@/features/files/api/filesApi', () => ({
  filesApi: { contentUrl: (_song: string, versionId: string, fileId: string) => `/content/${versionId}/${fileId}` },
}))

import GuitarProChangeSummary from '../components/GuitarProChangeSummary.vue'

const RIFF = ['0.6.4 2.6.4 3.6.4 0.6.4', '0.5.4 2.5.4 3.5.4 0.5.4', '0.4.4 2.4.4 3.4.4 0.4.4']

function gp(bars: string[], tempo = 90): Uint8Array {
  const score = importer.ScoreLoader.loadAlphaTex(`\\tempo ${tempo} \\track "Guitare" \\staff {tabs} ${bars.join(' | ')}`, new Settings())
  return new exporter.Gp7Exporter().export(score, new Settings())
}

const file = (id: string, name = 'riff.gp', fileType: SongFile['fileType'] = 'Tablature'): SongFile => ({
  id, originalFileName: name, contentType: 'application/octet-stream', sizeBytes: 3000,
  fileType, uploadedAt: '2026-10-21T10:00:00Z', preview: null, scan: 'Clean',
})
const version = (id: string, number: number, files: SongFile[]): SongVersion => ({
  id, songId: 's', number, title: `v${number}`, status: 'Demo', notes: null, lyrics: null, bpm: null, key: null,
  createdAt: '2026-10-21T10:00:00Z', updatedAt: '2026-10-21T10:00:00Z', files,
})

const fetchMock = vi.fn<typeof fetch>()
function serve(files: Record<string, Uint8Array | null>) {
  fetchMock.mockImplementation(async (url) => {
    const bytes = files[String(url).split('/').pop()!]
    return bytes ? new Response(bytes as Uint8Array<ArrayBuffer>) : new Response('', { status: 404 })
  })
}

beforeEach(() => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
})

async function mountSummary(filesA: SongFile[], fileB = file('fb')) {
  const wrapper = mount(GuitarProChangeSummary, {
    props: { songId: 's', a: version('va', 1, filesA), b: version('vb', 2, [fileB]), file: fileB },
    global: { stubs: { RouterLink: RouterLinkStub } },
  })
  await flushPromises()
  if (wrapper.find('[data-testid="gp-summary"]').exists()) {
    await vi.waitFor(() => expect(wrapper.find('[aria-busy="false"]').exists()).toBe(true))
  }
  return wrapper
}

describe('GuitarProChangeSummary (#36)', () => {
  it('résume les changements depuis le fichier de même nom dans A, et « Afficher les modifs » ouvre la tablature avec eux', async () => {
    const after = ['12.1.1', ...RIFF]
    after[3] = '0.4.4 2.4.4 5.4.4 0.4.4'
    serve({ other: gp(['3.1.4']), fa: gp(RIFF), fb: gp(after, 120) })
    const wrapper = await mountSummary([file('other', 'solo.gp'), file('fa', 'RIFF.gp')])

    expect(fetchMock).toHaveBeenCalledWith('/content/va/fa')
    expect(wrapper.findAll('li').map((li) => li.text())).toEqual([
      'Tempo : 90 → 120 BPM',
      'Guitare : 1 mesure modifiée, 1 mesure ajoutée',
    ])
    const link = wrapper.getComponent(RouterLinkStub)
    expect(link.text()).toBe('Afficher les modifs')
    expect(link.props('to')).toEqual({
      name: 'tablature', params: { songId: 's', versionId: 'vb', fileId: 'fb' }, query: { a: 'va', aFile: 'fa' },
    })
  })

  it('dit quand rien n’a changé, et garde le bouton', async () => {
    serve({ fa: gp(RIFF), fb: gp(RIFF) })
    const wrapper = await mountSummary([file('fa')])
    expect(wrapper.text()).toContain('Aucune différence de notes, de tempo ni d’accordage.')
    expect(wrapper.findComponent(RouterLinkStub).exists()).toBe(true)
  })

  it('ne montre rien si A n’a pas de fichier Guitar Pro, et annonce un fichier illisible', async () => {
    const none = await mountSummary([file('demo', 'demo.mp3', 'Audio')])
    expect(none.html()).toBe('<!--v-if-->')
    expect(fetchMock).not.toHaveBeenCalled()

    serve({ fa: gp(RIFF), fb: null })
    const broken = await mountSummary([file('fa')])
    expect(broken.get('[role="alert"]').text()).toBe('Impossible de comparer ces fichiers Guitar Pro.')
    expect(broken.findComponent(RouterLinkStub).exists()).toBe(false)
  })
})
