import { describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import StemMixer from '../components/StemMixer.vue'
import type { Mixer } from '../mixerEngine'
import { usePlayerStore } from '@/features/player/stores/playerStore'

function fakeEngine() {
  let playing = false
  const engine = {
    duration: 180,
    get isPlaying() { return playing },
    start: vi.fn(() => { playing = true; return 0.1 }),
    stop: vi.fn(() => { playing = false }),
    setGains: vi.fn(),
    position: vi.fn(() => 0),
    dispose: vi.fn(),
  }
  return engine satisfies Mixer
}

function mountMixer(engine = fakeEngine()) {
  const wrapper = mount(StemMixer, {
    props: { engine, names: ['Voix', 'Guitare', 'Batterie'] },
    global: { plugins: [createTestingPinia({ createSpy: vi.fn })] },
  })
  return { wrapper, engine }
}

const button = (w: ReturnType<typeof mountMixer>['wrapper'], label: string) => w.get(`button[aria-label="${label}"]`)

describe('StemMixer', () => {
  it('coupe une piste avec Muet', async () => {
    const { wrapper, engine } = mountMixer()
    expect(engine.setGains).toHaveBeenLastCalledWith([1, 1, 1])

    await button(wrapper, 'Couper Guitare').trigger('click')

    expect(engine.setGains).toHaveBeenLastCalledWith([1, 0, 1])
    expect(button(wrapper, 'Couper Guitare').attributes('aria-pressed')).toBe('true')
  })

  it('ne laisse entendre que la piste en solo, puis rend les autres', async () => {
    const { wrapper, engine } = mountMixer()

    await button(wrapper, 'Écouter Voix en solo').trigger('click')
    expect(engine.setGains).toHaveBeenLastCalledWith([1, 0, 0])

    await button(wrapper, 'Écouter Voix en solo').trigger('click')
    expect(engine.setGains).toHaveBeenLastCalledWith([1, 1, 1])
  })

  it('applique le volume d’une piste', async () => {
    const { wrapper, engine } = mountMixer()

    await wrapper.get('input[aria-label="Volume de Batterie"]').setValue('0.4')

    expect(engine.setGains).toHaveBeenLastCalledWith([1, 1, 0.4])
  })

  it('met le lecteur du bas en pause quand les pistes partent', async () => {
    const { wrapper, engine } = mountMixer()
    const player = usePlayerStore()

    await button(wrapper, 'Lire les pistes').trigger('click')

    expect(player.pause).toHaveBeenCalled()
    expect(engine.start).toHaveBeenCalledWith(0)
    expect(wrapper.find('button[aria-label="Mettre les pistes en pause"]').exists()).toBe(true)
  })
})