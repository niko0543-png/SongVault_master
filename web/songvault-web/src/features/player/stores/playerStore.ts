import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { Track } from '../track'

/** Ce dont le store a besoin d'un élément audio (HTMLAudioElement le satisfait ; un faux aussi). */
export interface AudioLike {
  src: string
  currentTime: number
  readonly duration: number
  play(): Promise<void>
  pause(): void
  addEventListener(type: string, listener: () => void): void
  removeEventListener(type: string, listener: () => void): void
}

export const usePlayerStore = defineStore('player', () => {
  const track = ref<Track | null>(null)
  const isPlaying = ref(false)
  const currentTime = ref(0)
  const duration = ref(0)
  const error = ref<string | null>(null)

  let audio: AudioLike | null = null          // privé et NON réactif
  let pendingStart: number | null = null      // position à appliquer quand la piste sera chargée

  const handlers: Record<string, () => void> = {
    play: () => { isPlaying.value = true },
    pause: () => { isPlaying.value = false },
    ended: () => { isPlaying.value = false },
    timeupdate: () => { currentTime.value = audio?.currentTime ?? 0 },
    loadedmetadata: () => {
      if (!audio) return
      duration.value = audio.duration
      if (pendingStart !== null) {
        audio.currentTime = pendingStart        // ne peut se faire qu'une fois la durée connue
        currentTime.value = pendingStart
        pendingStart = null
      }
    },
    error: () => {
      error.value = 'Impossible de lire ce fichier.'
      isPlaying.value = false
    },
  }

  function attach(element: AudioLike) {
    detach()
    audio = element
    for (const [type, handler] of Object.entries(handlers)) element.addEventListener(type, handler)
  }

  function detach() {
    if (!audio) return
    for (const [type, handler] of Object.entries(handlers)) audio.removeEventListener(type, handler)
    audio = null
  }

  function load(next: Track, startAt = 0) {
    if (!audio) return
    track.value = next
    error.value = null
    duration.value = 0
    currentTime.value = startAt
    pendingStart = startAt > 0 ? startAt : null
    audio.src = next.src
  }

  async function resume() {
    if (!audio) return
    try {
      await audio.play()
    } catch (e) {
      isPlaying.value = false
      error.value = e instanceof DOMException && e.name === 'NotAllowedError'
        ? 'Le navigateur a bloqué la lecture automatique : cliquez sur Lecture.'
        : 'Lecture impossible.'
    }
  }

  /** Lance une piste (ou reprend si c'est déjà la piste courante). */
  async function play(next: Track) {
    if (track.value?.src !== next.src) load(next)
    await resume()
  }

  function pause() {
    audio?.pause()
  }

  async function toggle() {
    if (isPlaying.value) pause()
    else await resume()
  }

  function seek(seconds: number) {
    if (!audio) return
    audio.currentTime = seconds
    currentTime.value = seconds
  }

  /** Change de piste en CONSERVANT la position (comparaison A/B). */
  async function switchTo(next: Track) {
    const position = audio?.currentTime ?? 0
    const wasPlaying = isPlaying.value
    load(next, position)
    if (wasPlaying) await resume()
  }

  /** Arrête la lecture et masque le lecteur (déconnexion). */
function stop() {
  audio?.pause()
  track.value = null
  currentTime.value = 0
  duration.value = 0
  error.value = null
}

  return { track, isPlaying, currentTime, duration, error, attach, detach, play, pause, toggle, seek, switchTo, stop }
})
