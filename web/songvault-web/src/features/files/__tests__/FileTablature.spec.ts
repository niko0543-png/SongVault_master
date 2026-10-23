import { describe, expect, it, vi } from 'vitest'
import { mount, RouterLinkStub } from '@vue/test-utils'
import FileList from '../components/FileList.vue'
import type { SongFile } from '../types'

vi.mock('../api/filesApi', () => ({ filesApi: { contentUrl: (_s: string, _v: string, id: string) => `/content/${id}`, waveform: vi.fn() } }))

// #28 : alphaTab ne doit pas être importé par la page de la version. Le faux le note s'il est chargé.
const loaded = vi.hoisted(() => ({ count: 0 }))
vi.mock('@/features/guitarpro/components/GuitarProViewer.vue', () => {
  loaded.count++
  return { default: {} }
})

const tab = (id: string, scan: SongFile['scan'] = 'Clean'): SongFile => ({
  id, originalFileName: `${id}.gp5`, contentType: 'application/octet-stream', sizeBytes: 1412,
  fileType: 'Tablature', uploadedAt: '2026-10-21T10:00:00Z', preview: null, scan,
})

const mountList = (files: SongFile[]) => mount(FileList, {
  props: { songId: 's1', versionId: 'v1', files },
  global: { stubs: { RouterLink: RouterLinkStub, FileWaveform: true } },
})

describe('FileList, tablatures Guitar Pro (#28, #32)', () => {
  it('ouvre la tablature sur la page du lecteur, sans charger alphaTab ici', () => {
    const wrapper = mountList([tab('riff')])

    const link = wrapper.getComponent(RouterLinkStub)
    expect(link.text()).toBe('Ouvrir la tablature')
    expect(link.props('to')).toEqual({ name: 'tablature', params: { songId: 's1', versionId: 'v1', fileId: 'riff' } })
    expect(loaded.count).toBe(0)
  })

  it('ne propose pas de tablature pour un fichier non déclaré sain, ni pour un fichier audio', () => {
    const audio: SongFile = { ...tab('maquette'), originalFileName: 'maquette.mp3', fileType: 'Audio' }
    const wrapper = mountList([tab('a'), tab('b', 'Infected'), audio])
    expect(wrapper.findAll('[data-testid="show-tablature"]')).toHaveLength(1)
  })
})
