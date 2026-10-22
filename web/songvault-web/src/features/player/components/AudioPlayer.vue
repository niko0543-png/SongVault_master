<script setup lang="ts">
import { useTemplateRef, watch } from 'vue'
import { storeToRefs } from 'pinia'
import { usePlayerStore } from '../stores/playerStore'
import { useAudioPlayer } from '../composables/useAudioPlayer'
import { SKIP_SECONDS, useMediaSession } from '../composables/useMediaSession'
import { formatTime } from '@/shared/utils/format'
import { onBeforeUnmount, onMounted } from 'vue'
import { isGlobalShortcut } from '@/shared/utils/keyboard'
import { t } from '@/shared/i18n'

const audioElement = useTemplateRef<HTMLAudioElement>('audio-element')
useAudioPlayer(audioElement)
useMediaSession()                                   // #14 : commandes de l'écran verrouillé

const player = usePlayerStore()
const { track, isPlaying, currentTime, duration, error } = storeToRefs(player)

function onSeek(event: Event) {
  player.seek(Number((event.target as HTMLInputElement).value))
}

function onKeydown(event: KeyboardEvent) {
  if (!track.value || !isGlobalShortcut(event)) return
  if (event.key === ' ' || event.key === 'k') {
    event.preventDefault()                         // sinon Espace fait aussi défiler la page
    void player.toggle()
  }
}
onMounted(() => window.addEventListener('keydown', onKeydown))
onBeforeUnmount(() => window.removeEventListener('keydown', onKeydown))

// #31 : la hauteur de la barre, pour que le panneau des pistes de la tablature se pose juste au-dessus
const bar = useTemplateRef<HTMLElement>('player-bar')
let observer: ResizeObserver | null = null
const rootStyle = () => document.documentElement.style
watch(bar, (element) => {
  observer?.disconnect()
  observer = null
  if (!element) {
    rootStyle().removeProperty('--audio-player-height')
    return
  }
  if (typeof ResizeObserver === 'undefined') return
  observer = new ResizeObserver(() => rootStyle().setProperty('--audio-player-height', `${element.offsetHeight}px`))
  observer.observe(element)
})
onBeforeUnmount(() => {
  observer?.disconnect()
  rootStyle().removeProperty('--audio-player-height')
})
</script>

<template>
  <!-- TOUJOURS rendu (même sans piste) : c'est l'unique <audio> de l'application -->
  <audio ref="audio-element" preload="metadata" />

  <div v-if="track" ref="player-bar" class="player" role="region" :aria-label="t('player.audio.label')">
    <div class="controls">
      <button type="button" class="secondary skip" :aria-label="t('player.audio.back', { skipSeconds: SKIP_SECONDS })"
              :disabled="!duration" @click="player.skip(-SKIP_SECONDS)">−{{ SKIP_SECONDS }}</button>
      <button type="button" class="play" :aria-label="isPlaying ? t('player.audio.pause') : t('player.audio.play')"
          aria-keyshortcuts="Space k" :title="isPlaying ? t('player.audio.pauseHint') : t('player.audio.playHint')" @click="player.toggle()">
        {{ isPlaying ? '⏸' : '▶' }}
      </button>
      <button type="button" class="secondary skip" :aria-label="t('player.audio.forward', { skipSeconds: SKIP_SECONDS })"
              :disabled="!duration" @click="player.skip(SKIP_SECONDS)">+{{ SKIP_SECONDS }}</button>
    </div>
    <div class="info">
      <strong>{{ track.title }}</strong>
      <small class="muted">{{ track.subtitle }}</small>
    </div>
    <input
      class="seek" type="range" min="0" step="0.1"
      :max="duration || 0" :value="currentTime" :disabled="!duration"
      :aria-label="t('player.audio.position')"
      :aria-valuetext="t('shared.time.positionOf', { position: formatTime(currentTime), duration: formatTime(duration) })"
      @input="onSeek"
    />
    <span class="time">{{ formatTime(currentTime) }} / {{ formatTime(duration) }}</span>
    <p v-if="error" class="field-error error" role="alert">{{ error }}</p>
  </div>
</template>

<style scoped>
/* #14 : le bas de l'écran reste libre pour la barre d'accueil de l'iPhone (safe-area) */
.player { position: fixed; left: 0; right: 0; bottom: 0; z-index: 40; display: grid;
          grid-template-columns: auto minmax(8rem, 1fr) 3fr auto; gap: 1rem; align-items: center;
          padding: .75rem 1.5rem calc(.75rem + env(safe-area-inset-bottom));
          background: var(--surface); border-top: 1px solid var(--border); }
.controls { display: flex; align-items: center; gap: .5rem; }
.play { width: 2.75rem; height: 2.75rem; border-radius: 50%; padding: 0; }
.skip { padding: .25rem .5rem; font-variant-numeric: tabular-nums; }
.info { display: grid; min-width: 0; }
.info > * { overflow: hidden; white-space: nowrap; text-overflow: ellipsis; }
.seek { width: 100%; padding: 0; }
.time { font-variant-numeric: tabular-nums; }
.error { grid-column: 1 / -1; }

/* #14 : téléphone. Ligne 1 : titre et temps ; ligne 2 : la barre de position, pleine largeur ;
   ligne 3 : les trois boutons, centrés sous le pouce */
@media (max-width: 720px) {
  .player { grid-template-columns: minmax(0, 1fr) auto; gap: .25rem .75rem; padding: .5rem 1rem calc(.5rem + env(safe-area-inset-bottom)); }
  .info { grid-column: 1; grid-row: 1; }
  .time { grid-column: 2; grid-row: 1; font-size: .9rem; }
  .seek { grid-column: 1 / -1; grid-row: 2; height: 32px; }
  .controls { grid-column: 1 / -1; grid-row: 3; justify-content: center; gap: 1.5rem; }
  .play { width: 3.25rem; height: 3.25rem; font-size: 1.25rem; }
  .skip { min-width: 3rem; }
}
</style>