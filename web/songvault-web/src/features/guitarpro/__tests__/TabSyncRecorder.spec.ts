import { describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { nextTick } from 'vue'
import { usePlayerStore } from '@/features/player/stores/playerStore'
import TabSyncRecorder from '../components/TabSyncRecorder.vue'

/** #29 : calage au tapé. jsdom ne joue pas de son : l'élément <audio> est piloté à la main (instant, lecture, pause). */
async function mountRecorder() {
  const wrapper = mount(TabSyncRecorder, {
    props: {
      audios: [{ id: 'a1', name: 'maquette.wav' }, { id: 'a2', name: 'live.mp3' }],
      initialAudioId: 'a2',
      src: (id: string) => `/listen/${id}`,
      bars: 3,
    },
    global: { plugins: [createTestingPinia({ createSpy: vi.fn })] },
    attachTo: document.body,
  })
  const audio = wrapper.get('[data-testid="sync-audio"]').element as HTMLAudioElement
  let paused = true
  let time = 0
  Object.defineProperty(audio, 'paused', { get: () => paused })
  Object.defineProperty(audio, 'currentTime', { get: () => time, set: (value: number) => { time = value } })
  audio.play = vi.fn(async () => { paused = false; audio.dispatchEvent(new Event('play')) })
  audio.pause = vi.fn(() => { paused = true; audio.dispatchEvent(new Event('pause')) })
  const at = async (seconds: number) => {
    time = seconds
    await wrapper.get('[data-testid="sync-tap"]').trigger('click')
  }
  return { wrapper, audio, at }
}

describe('TabSyncRecorder (#29)', () => {
  it('propose la maquette du calage actuel, et part du début de la maquette', async () => {
    const { wrapper, audio } = await mountRecorder()

    expect(audio.getAttribute('src')).toBe('/listen/a2')
    audio.currentTime = 12
    await wrapper.get('[data-testid="sync-play"]').trigger('click')
    await flushPromises()

    expect(audio.currentTime).toBe(0)
    expect(wrapper.get('[data-testid="sync-play"]').text()).toBe('Mettre la maquette en pause')
  })

  it('note l’instant de chaque tape, montre la mesure suivante, et s’arrête à la dernière', async () => {
    const { wrapper, audio, at } = await mountRecorder()
    expect(wrapper.get('[data-testid="sync-tap"]').attributes('disabled')).toBeDefined()   // maquette à l'arrêt
    await wrapper.get('[data-testid="sync-play"]').trigger('click')
    await flushPromises()

    await at(1.0004)
    await at(1.0004)                                           // double appui : ignoré
    await at(2.5)
    expect(wrapper.get('[data-testid="sync-tap"]').text()).toBe('Début de la mesure 3 sur 3')
    expect(wrapper.emitted('next')!.map(([i]) => i)).toEqual([0, 1, 2])
    await at(4.1)

    expect(wrapper.get('[data-testid="sync-count"]').text()).toBe('3 mesures calées sur 3.')
    expect(wrapper.get('[data-testid="sync-tap"]').text()).toBe('Toutes les mesures sont calées')
    expect(audio.pause).toHaveBeenCalled()
    await wrapper.get('[data-testid="sync-save"]').trigger('click')
    expect(wrapper.emitted('save')![0]).toEqual(['a2', [1000, 2500, 4100]])
  })

  it('annule la dernière tape, recommence, et repart de zéro avec une autre maquette', async () => {
    const { wrapper, at } = await mountRecorder()
    await wrapper.get('[data-testid="sync-play"]').trigger('click')
    await flushPromises()
    await at(1)
    await at(2)

    await wrapper.findAll('button').find((b) => b.text() === 'Annuler la dernière')!.trigger('click')
    expect(wrapper.get('[data-testid="sync-count"]').text()).toBe('1 mesure calée sur 3.')
    await wrapper.findAll('button').find((b) => b.text() === 'Recommencer')!.trigger('click')
    expect(wrapper.get('[data-testid="sync-count"]').text()).toBe('Aucune mesure calée sur 3.')

    await wrapper.get('[data-testid="sync-play"]').trigger('click')
    await flushPromises()
    await at(1)
    await wrapper.get('[data-testid="sync-audio-choice"]').setValue('a1')
    expect(wrapper.get('[data-testid="sync-count"]').text()).toBe('Aucune mesure calée sur 3.')
    expect(wrapper.get('[data-testid="sync-save"]').attributes('disabled')).toBeDefined()
  })

  it('un seul son à la fois avec le lecteur du bas', async () => {
    const { wrapper, audio } = await mountRecorder()
    const player = usePlayerStore()

    await wrapper.get('[data-testid="sync-play"]').trigger('click')
    await flushPromises()
    expect(player.pause).toHaveBeenCalled()

    player.isPlaying = true
    await nextTick()
    expect(audio.pause).toHaveBeenCalled()
  })
})