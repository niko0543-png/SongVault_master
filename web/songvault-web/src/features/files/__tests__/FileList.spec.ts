import { describe, expect, it, vi } from 'vitest'
import { mount, RouterLinkStub } from '@vue/test-utils'
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
    global: { stubs: { FileWaveform: true, RouterLink: RouterLinkStub } },
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

describe('FileList, analyse antivirus (#17)', () => {
  const scanned = (id: string, scan: SongFile['scan'], scanThreat: string | null = null): SongFile =>
    ({ ...audio(id, 'Ready'), scan, scanThreat })

  it("n'offre ni écoute ni téléchargement tant que le fichier n'est pas déclaré sain", () => {
    const wrapper = mountList([scanned('pending', 'Pending'), scanned('infected', 'Infected', 'Win.Test.EICAR_HDB-1'),
      scanned('failed', 'Failed'), scanned('ready', 'Clean')])

    const items = wrapper.findAll('[data-testid="file-item"]')
    expect(items[0]!.text()).toContain('Analyse antivirus en cours…')
    expect(items[1]!.get('[data-testid="scan-infected"]').text()).toContain('Win.Test.EICAR_HDB-1')
    expect(items[2]!.text()).toContain("L'analyse antivirus a échoué")
    for (const item of items.slice(0, 3)) {
      expect(item.text()).not.toContain('Écouter')
      expect(item.find('a[download]').exists()).toBe(false)
      expect(item.text()).toContain('Supprimer')                  // un fichier bloqué se supprime
    }
    expect(items[3]!.text()).toContain('Écouter')
    expect(wrapper.findAllComponents({ name: 'FileWaveform' })).toHaveLength(1)
  })

  it("s'écoute et se télécharge sans analyse quand l'antivirus est désactivé", () => {
    const wrapper = mountList([scanned('free', 'NotScanned')])

    const item = wrapper.get('[data-testid="file-item"]')
    expect(item.text()).toContain('Écouter')
    expect(item.find('a[download]').exists()).toBe(true)
    expect(item.text()).not.toContain('Analyse antivirus')
  })

  it("propose de relancer une analyse en échec, sauf en lecture seule", async () => {
    const failed = scanned('failed', 'Failed')
    const wrapper = mountList([failed])

    await wrapper.get('button.link').trigger('click')

    expect(wrapper.emitted('retryScan')).toEqual([[failed]])
    expect(mountList([failed], true).find('button.link').exists()).toBe(false)
  })
})
describe('FileList, découpage (ADR 0050)', () => {
  it("propose de découper un fichier audio à l'aperçu prêt, sauf en lecture seule", () => {
    const wrapper = mountList([audio('pending', 'Pending'), audio('ready', 'Ready')])

    const links = wrapper.findAll('[data-testid="split-file"]')
    expect(links).toHaveLength(1)
    expect(wrapper.getComponent(RouterLinkStub).props('to')).toEqual({
      name: 'split-recording', params: { songId: 's1', versionId: 'v1', fileId: 'ready' },
    })
    expect(mountList([audio('ready', 'Ready')], true).find('[data-testid="split-file"]').exists()).toBe(false)
  })
})
