import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import FileWaveform from '../components/FileWaveform.vue'
import { filesApi } from '../api/filesApi'

vi.mock('../api/filesApi', () => ({ filesApi: { waveform: vi.fn() } }))

async function mountWaveform(progress = 0) {
  const wrapper = mount(FileWaveform, {
    props: { songId: 's1', versionId: 'v1', fileId: 'f1', label: 'prise.wav', progress },
    attachTo: document.body,
  })
  await flushPromises()
  return wrapper
}

describe('FileWaveform', () => {
  beforeEach(() => {
    vi.mocked(filesApi.waveform).mockReset()
      .mockResolvedValue({ durationMs: 200_000, peaks: Array.from({ length: 1000 }, () => 128) })
  })

  it('dessine 200 barres et colore la partie écoutée', async () => {
    const wrapper = await mountWaveform(0.25)

    expect(filesApi.waveform).toHaveBeenCalledWith('s1', 'v1', 'f1')
    const slider = wrapper.get('[role="slider"]')
    expect(slider.attributes('aria-valuenow')).toBe('50')
    expect(slider.attributes('aria-valuetext')).toBe('0:50 sur 3:20')
    expect(wrapper.get('svg').attributes('viewBox')).toBe('0 0 200 40')
    expect(wrapper.get('clipPath rect').attributes('width')).toBe('50')
    wrapper.unmount()
  })

  it("un clic demande la position correspondante", async () => {
    const wrapper = await mountWaveform()
    const slider = wrapper.get('[role="slider"]')
    vi.spyOn(slider.element, 'getBoundingClientRect').mockReturnValue({ left: 100, width: 400 } as DOMRect)

    await slider.trigger('click', { clientX: 200 })

    expect(wrapper.emitted('seek')).toEqual([[50]])
    wrapper.unmount()
  })

  it('les flèches avancent ou reculent de 5 secondes, sans sortir du fichier', async () => {
    const wrapper = await mountWaveform(0.01)        // 2 s
    const slider = wrapper.get('[role="slider"]')

    await slider.trigger('keydown', { key: 'ArrowRight' })
    await slider.trigger('keydown', { key: 'ArrowLeft' })
    await slider.trigger('keydown', { key: 'End' })

    expect(wrapper.emitted('seek')).toEqual([[7], [0], [200]])
    wrapper.unmount()
  })

  it("n'affiche rien si la forme d'onde ne se charge pas", async () => {
    vi.mocked(filesApi.waveform).mockRejectedValue(new Error('404'))

    const wrapper = await mountWaveform()

    expect(wrapper.find('[role="slider"]').exists()).toBe(false)
    wrapper.unmount()
  })
})