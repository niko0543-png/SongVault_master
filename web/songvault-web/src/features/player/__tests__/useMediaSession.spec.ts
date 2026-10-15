import { beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, nextTick } from 'vue'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { usePlayerStore, type AudioLike } from '../stores/playerStore'
import { SKIP_SECONDS, useMediaSession } from '../composables/useMediaSession'

class FakeAudio extends EventTarget implements AudioLike {
  src = ''
  currentTime = 0
  duration = 240
  play = vi.fn<() => Promise<void>>(async () => { this.dispatchEvent(new Event('play')) })
  pause = vi.fn<() => void>(() => { this.dispatchEvent(new Event('pause')) })
}

/** Faux navigator.mediaSession : garde les gestionnaires pour les déclencher comme l'écran verrouillé. */
function fakeSession() {
  const handlers = new Map<MediaSessionAction, MediaSessionActionHandler | null>()
  return {
    handlers,
    metadata: null as MediaMetadata | null,
    playbackState: 'none' as MediaSessionPlaybackState,
    setActionHandler: vi.fn((action: MediaSessionAction, handler: MediaSessionActionHandler | null) => { handlers.set(action, handler) }),
    setPositionState: vi.fn(),
  }
}

// jsdom n'a pas MediaMetadata : une classe qui garde ce qu'on lui donne suffit
class FakeMetadata { constructor(init: MediaMetadataInit) { Object.assign(this, init) } }

describe('useMediaSession', () => {
  let audio: FakeAudio
  let session: ReturnType<typeof fakeSession>

  beforeEach(() => {
    setActivePinia(createPinia())
    vi.stubGlobal('MediaMetadata', FakeMetadata)
    audio = new FakeAudio()
    usePlayerStore().attach(audio)
    session = fakeSession()
  })

  const mountWith = () => mount(defineComponent({
    setup: () => { useMediaSession(session as unknown as MediaSession) },
    template: '<div />',
  }))

  it("montre la piste sur l'écran verrouillé et suit la lecture", async () => {
    mountWith()
    const player = usePlayerStore()

    await player.play({ id: 'a', title: 'Nocturne — v2', subtitle: 'mix.mp3', src: '/api/a' })
    await nextTick()

    expect(session.metadata).toMatchObject({ title: 'Nocturne — v2', artist: 'mix.mp3' })
    expect(session.playbackState).toBe('playing')
    player.pause()
    await nextTick()
    expect(session.playbackState).toBe('paused')
  })

  it("les commandes de l'écran verrouillé pilotent le lecteur", async () => {
    mountWith()
    const player = usePlayerStore()
    await player.play({ id: 'a', title: 'Nocturne', subtitle: 'mix.mp3', src: '/api/a' })
    audio.dispatchEvent(new Event('loadedmetadata'))
    audio.currentTime = 60

    session.handlers.get('seekforward')!({ action: 'seekforward' })
    expect(audio.currentTime).toBe(60 + SKIP_SECONDS)
    session.handlers.get('seekbackward')!({ action: 'seekbackward', seekOffset: 30 })
    expect(audio.currentTime).toBe(40)
    session.handlers.get('seekto')!({ action: 'seekto', seekTime: 120 })
    expect(audio.currentTime).toBe(120)
    session.handlers.get('pause')!({ action: 'pause' })
    expect(audio.pause).toHaveBeenCalled()
  })

  it('retire ses gestionnaires quand le lecteur disparaît', () => {
    const wrapper = mountWith()

    wrapper.unmount()

    expect(session.handlers.get('play')).toBeNull()
    expect(session.metadata).toBeNull()
  })
})