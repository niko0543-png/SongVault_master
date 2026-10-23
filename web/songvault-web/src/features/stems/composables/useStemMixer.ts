import { onScopeDispose, ref, shallowRef } from 'vue'
import { createMixer, type Mixer } from '../mixerEngine'
import type { Stem } from '../types'
import { t } from '@/shared/i18n'

export type MixerState = 'idle' | 'loading' | 'ready' | 'error'

/**
 * Charge les pistes dans un AudioContext (#23) : chaque aperçu MP3 est téléchargé puis décodé en entier.
 * load() doit partir d'un clic : le navigateur n'autorise le son qu'après un geste de l'utilisateur.
 * L'adresse d'écoute redirige vers le stockage en production (ADR 0012) : fetch suit la redirection.
 */
export function useStemMixer(urlOf: (stem: Stem) => string) {
  const state = ref<MixerState>('idle')
  const error = ref<string | null>(null)
  const loaded = ref(0)
  const engine = shallowRef<Mixer | null>(null)
  let ctx: AudioContext | null = null

  async function decode(audio: AudioContext, stem: Stem) {
    const response = await fetch(urlOf(stem))
    if (!response.ok) throw new Error(t('stems.engine.loadFailed', { name: stem.name, status: response.status }))
    const buffer = await audio.decodeAudioData(await response.arrayBuffer())
    loaded.value++
    return buffer
  }

  async function load(stems: readonly Stem[]) {
    dispose()
    state.value = 'loading'
    const audio = new AudioContext()
    ctx = audio
    try {
      const buffers = await Promise.all(stems.map((s) => decode(audio, s)))
      if (ctx !== audio) return                        // déchargé pendant le chargement
      engine.value = createMixer(audio, buffers)
      state.value = 'ready'
    } catch (e) {
      if (ctx !== audio) return
      dispose()
      state.value = 'error'
      error.value = e instanceof Error ? e.message : t('stems.engine.loadError')
    }
  }

  function dispose() {
    engine.value?.dispose()
    engine.value = null
    void ctx?.close()
    ctx = null
    state.value = 'idle'
    error.value = null
    loaded.value = 0
  }

  onScopeDispose(dispose)
  return { state, error, loaded, engine, load, dispose }
}