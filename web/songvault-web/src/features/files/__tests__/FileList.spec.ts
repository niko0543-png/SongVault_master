import { describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import FileList from '../components/FileList.vue'
import type { SongFile } from '../types'

vi.mock('../api/filesApi', () => ({ filesApi: { contentUrl: () => '/content', waveform: vi.fn() } }))

const audio = (id: string, preview: SongFile['preview']): SongFile => ({
  id, originalFileName: `${id}.wav`, contentType: 'audio/wav', sizeBytes: 10,
  fileType: 'Audio', uploadedAt: '2026-10-15T10:00:00Z', preview,
})

function mountList(files: SongFile[], readonly = false) {
  return mount(FileList, {
    props: { songId: 's1', versionId: 'v1', files, readonly, playing: { fileId: 'ready', progress: 0.5 } },
    global: { stubs: { FileWaveform: true } },
  })
}

describe('FileList, aperçus (#15)', () => {
  it("affiche l'état de chaque aperçu et la forme d'onde des seuls aperçus prêts", () => {
    const wrapper = mountList([audio('pending', 'Pending'), audio('failed', 'Failed'), audio('ready', 'Ready')])

    const items = wrapper.findAll('[data-testid="file-item"]')
    expect(items[0]!.text()).toContain("Préparation de l'aperçu…")
    expect(items[1]!.text()).toContain('Aperçu indisponible')
    const waveforms = wrapper.findAllComponents({ name: 'FileWaveform' })
    expect(waveforms).toHaveLength(1)
    expect(waveforms[0]!.props('progress')).toBe(0.5)
  })

  it('propose de réessayer un aperçu en échec, sauf en lecture seule', async () => {
    const failed = audio('failed', 'Failed')
    const wrapper = mountList([failed])

    await wrapper.get('button.link').trigger('click')

    expect(wrapper.emitted('retry')).toEqual([[failed]])
    expect(mountList([failed], true).find('button.link').exists()).toBe(false)
  })
})