import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { usePlayerStore, type AudioLike } from '../stores/playerStore'
import type { Track } from '../track'

/** Faux <audio> : EventTarget fournit addEventListener et dispatchEvent. */
class FakeAudio extends EventTarget implements AudioLike {
  src = ''
  currentTime = 0
  duration = 240
  play = vi.fn<() => Promise<void>>(async () => { this.dispatchEvent(new Event('play')) })
  pause = vi.fn<() => void>(() => { this.dispatchEvent(new Event('pause')) })
}

const trackA: Track = { id: 'a', title: 'Nocturne — v1', subtitle: 'v1.mp3', src: '/api/a' }
const trackB: Track = { id: 'b', title: 'Nocturne — v2', subtitle: 'v2.mp3', src: '/api/b' }

describe('playerStore', () => {
  let audio: FakeAudio
  let player: ReturnType<typeof usePlayerStore>

  beforeEach(() => {
    setActivePinia(createPinia())
    audio = new FakeAudio()
    player = usePlayerStore()
    player.attach(audio)
  })

  it('charge la piste et lance la lecture', async () => {
    await player.play(trackA)

    expect(audio.src).toBe('/api/a')
    expect(audio.play).toHaveBeenCalledOnce()
    expect(player.isPlaying).toBe(true)
    expect(player.track?.id).toBe('a')
  })

  it('la bascule A/B conserve la position et continue la lecture', async () => {
    await player.play(trackA)
    audio.currentTime = 42                                // on écoute A depuis 42 s

    await player.switchTo(trackB)
    audio.currentTime = 0                                 // comme un vrai navigateur : nouvelle source → retour à 0
    audio.dispatchEvent(new Event('loadedmetadata'))      // le navigateur a chargé B

    expect(audio.src).toBe('/api/b')
    expect(audio.currentTime).toBe(42)
    expect(audio.play).toHaveBeenCalledTimes(2)
  })

  it('signale une lecture bloquée par le navigateur', async () => {
    audio.play.mockImplementation(async () => {
      throw new DOMException('Lecture bloquée', 'NotAllowedError')
    })

    await player.play(trackA)

    expect(player.isPlaying).toBe(false)
    expect(player.error).toContain('bloqué')
  })

  it('detach retire les écouteurs', async () => {
    player.detach()
    audio.dispatchEvent(new Event('play'))
    expect(player.isPlaying).toBe(false)
  })
})
