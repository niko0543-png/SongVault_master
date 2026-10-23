import { afterEach, describe, expect, it, vi } from 'vitest'
import { mount, RouterLinkStub } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'

/** #34 : l'en-tête de la version ouvre directement ses tablatures lisibles. */
const state = vi.hoisted(() => ({ files: [] as unknown[] }))
vi.mock('../composables/useVersion', async () => {
  const { ref } = await import('vue')
  return {
    useVersion: () => ({
      song: ref({ title: 'Riff' }),
      version: ref({ id: 'v1', number: 2, title: 'Démo', status: 'Idea', notes: null, lyrics: null, bpm: null, key: null,
                     createdAt: '2026-10-22T10:00:00Z', updatedAt: '2026-10-22T10:00:00Z', files: state.files }),
      previous: ref(null), next: ref(null), isLoading: ref(false), error: ref(null),
      isSaving: ref(false), saveError: ref(null), save: vi.fn(), reload: vi.fn(),
    }),
  }
})

import VersionDetailPage from '../pages/VersionDetailPage.vue'

afterEach(() => vi.unstubAllGlobals())

const file = (id: string, fileType: string, scan = 'Clean') =>
  ({ id, originalFileName: `${id}.gp`, contentType: 'x', sizeBytes: 1, fileType, uploadedAt: '2026-10-22T10:00:00Z', preview: null, scan })

function mountPage() {
  vi.stubGlobal('fetch', vi.fn(async () => new Response('[]', { headers: { 'Content-Type': 'application/json' } })))
  return mount(VersionDetailPage, {
    props: { songId: 's1', versionId: 'v1' },
    global: {
      plugins: [createTestingPinia({ createSpy: vi.fn, initialState: { band: { activeId: 'b1' } } })],
      stubs: { RouterLink: RouterLinkStub, FileList: true, FileUploader: true, StemsSection: true, ShareLinksPanel: true,
               CommentsPanel: true, LyricsPanel: true },
    },
  })
}

describe('VersionDetailPage, accès direct aux tablatures (#34)', () => {
  it('un lien « Tablature » vers la page du lecteur, un par fichier s’il y en a plusieurs', () => {
    state.files = [file('riff', 'Tablature'), file('demo', 'Audio'), file('virus', 'Tablature', 'Infected')]
    const links = mountPage().findAll('[data-testid="open-tablature"]')
    expect(links).toHaveLength(1)
    expect(links[0]!.text()).toBe('🎸 Tablature')
    expect(links[0]!.findComponent(RouterLinkStub).props('to'))
      .toEqual({ name: 'tablature', params: { songId: 's1', versionId: 'v1', fileId: 'riff' } })

    state.files = [file('riff', 'Tablature'), file('solo', 'Tablature')]
    expect(mountPage().findAll('[data-testid="open-tablature"]').map((l) => l.text()))
      .toEqual(['🎸 Tablature : riff.gp', '🎸 Tablature : solo.gp'])
  })
})
