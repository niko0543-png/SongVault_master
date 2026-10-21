import { describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import FileList from '../components/FileList.vue'
import type { SongFile } from '../types'

vi.mock('../api/filesApi', () => ({ filesApi: { contentUrl: (_s: string, _v: string, id: string) => `/content/${id}`, waveform: vi.fn() } }))

// #28 : le lecteur et alphaTab ne doivent être importés qu'au clic. Le faux le note quand il est chargé.
const loaded = vi.hoisted(() => ({ count: 0 }))
vi.mock('@/features/guitarpro/components/GuitarProViewer.vue', () => {
  loaded.count++
  return {
    __esModule: true,                                              // defineAsyncComponent prend alors default
    default: defineComponent({
      name: 'GuitarProViewer',
      props: { src: String, label: String },
      setup: (props) => () => h('div', { 'data-testid': 'viewer' }, props.src),
    }),
  }
})

const tab = (id: string, scan: SongFile['scan'] = 'Clean'): SongFile => ({
  id, originalFileName: `${id}.gp5`, contentType: 'application/octet-stream', sizeBytes: 1412,
  fileType: 'Tablature', uploadedAt: '2026-10-21T10:00:00Z', preview: null, scan,
})

describe('FileList, tablatures Guitar Pro (#28)', () => {
  it('ne charge le lecteur qu’au clic sur « Afficher la tablature », et le referme', async () => {
    const wrapper = mount(FileList, { props: { songId: 's1', versionId: 'v1', files: [tab('riff')] } })
    await flushPromises()
    expect(loaded.count).toBe(0)

    const button = wrapper.get('[data-testid="show-tablature"]')
    expect(button.text()).toBe('Afficher la tablature')
    expect(button.attributes('aria-expanded')).toBe('false')

    await button.trigger('click')
    await flushPromises()
    expect(loaded.count).toBe(1)
    expect(wrapper.get('[data-testid="viewer"]').text()).toBe('/content/riff')
    expect(button.text()).toBe('Masquer la tablature')
    expect(button.attributes('aria-expanded')).toBe('true')

    await button.trigger('click')
    expect(wrapper.find('[data-testid="viewer"]').exists()).toBe(false)
  })

  it('n’ouvre qu’une tablature à la fois, et aucune pour un fichier non déclaré sain', async () => {
    const wrapper = mount(FileList, { props: { songId: 's1', versionId: 'v1', files: [tab('a'), tab('b'), tab('c', 'Infected')] } })
    const buttons = wrapper.findAll('[data-testid="show-tablature"]')
    expect(buttons).toHaveLength(2)                                  // le fichier infecté n'a pas le bouton

    await buttons[0]!.trigger('click')
    await buttons[1]!.trigger('click')
    await flushPromises()
    expect(wrapper.findAll('[data-testid="viewer"]').map((v) => v.text())).toEqual(['/content/b'])
  })

  it('ne propose pas de tablature pour un fichier audio', () => {
    const audio: SongFile = { ...tab('maquette'), originalFileName: 'maquette.mp3', fileType: 'Audio' }
    const wrapper = mount(FileList, { props: { songId: 's1', versionId: 'v1', files: [audio] }, global: { stubs: { FileWaveform: true } } })
    expect(wrapper.find('[data-testid="show-tablature"]').exists()).toBe(false)
  })
})