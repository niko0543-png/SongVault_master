import { describe, expect, it, vi } from 'vitest'
import { mount, RouterLinkStub } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import type { SongFile } from '@/features/files/types'

/** #32 : la page du lecteur, avec une version et un lecteur factices. */
const state = vi.hoisted(() => ({ files: [] as unknown[], canWrite: true }))
vi.mock('@/features/versions/composables/useVersion', async () => {
  const { ref } = await import('vue')
  return {
    useVersion: () => ({
      song: ref({ title: 'Riff' }),
      version: ref({ id: 'v1', title: 'Démo', files: state.files }),
      isLoading: ref(false), error: ref(null), reload: vi.fn(),
    }),
  }
})
vi.mock('@/features/bands/stores/bandStore', () => ({ useBandStore: () => ({ can: () => state.canWrite }) }))
vi.mock('@/features/files/api/filesApi', () => ({ filesApi: { contentUrl: (_s: string, _v: string, id: string) => `/content/${id}` } }))
vi.mock('../components/GuitarProViewer.vue', () => ({
  default: defineComponent({
    name: 'GuitarProViewer',
    props: { src: String, label: String, sync: Object, fill: Boolean },
    setup: (props) => () => h('div', { 'data-testid': 'viewer', 'data-fill': String(props.fill), 'data-sync': JSON.stringify(props.sync) }, props.src),
  }),
}))

import TablaturePage from '../pages/TablaturePage.vue'

const file = (id: string, fileType: SongFile['fileType'], scan: SongFile['scan'] = 'Clean') =>
  ({ id, originalFileName: `${id}.${fileType === 'Audio' ? 'mp3' : 'gp'}`, fileType, scan })

const mountPage = (fileId = 'riff') => mount(TablaturePage, {
  props: { songId: 's1', versionId: 'v1', fileId },
  global: { stubs: { RouterLink: RouterLinkStub } },
})

describe('TablaturePage (#32)', () => {
  it('ouvre le lecteur en plein cadre, avec les maquettes lisibles de la version et le droit de caler', () => {
    for (const canWrite of [true, false]) {
      state.canWrite = canWrite
      state.files = [file('riff', 'Tablature'), file('demo', 'Audio'), file('virus', 'Audio', 'Infected')]
      const wrapper = mountPage()

      const viewer = wrapper.get('[data-testid="viewer"]')
      expect(viewer.text()).toBe('/content/riff')
      expect(viewer.attributes('data-fill')).toBe('true')
      expect(JSON.parse(viewer.attributes('data-sync')!)).toEqual({
        songId: 's1', versionId: 'v1', fileId: 'riff', audios: [{ id: 'demo', name: 'demo.mp3' }], canEdit: canWrite,
      })
      expect(wrapper.get('h1').text()).toBe('riff.gp')
      expect(wrapper.getComponent(RouterLinkStub).props('to')).toEqual({ name: 'version-detail', params: { songId: 's1', versionId: 'v1' } })
      expect(document.documentElement.classList.contains('tablature-open')).toBe(true)   // la page derrière ne défile pas
      wrapper.unmount()
      expect(document.documentElement.classList.contains('tablature-open')).toBe(false)
    }
  })

  it('annonce une tablature absente de la version, ou pas encore déclarée saine', () => {
    state.files = [file('demo', 'Audio'), file('pending', 'Tablature', 'Pending')]
    expect(mountPage('riff').get('[role="alert"]').text()).toBe('Cette tablature n’est plus dans la version.')
    expect(mountPage('demo').get('[role="alert"]').text()).toBe('Cette tablature n’est plus dans la version.')
    const pending = mountPage('pending')
    expect(pending.get('[role="alert"]').text()).toContain('pas encore lisible')
    expect(pending.find('[data-testid="viewer"]').exists()).toBe(false)
  })
})
