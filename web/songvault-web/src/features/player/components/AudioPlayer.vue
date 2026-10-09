<script setup lang="ts">
import { useTemplateRef } from 'vue'
import { storeToRefs } from 'pinia'
import { usePlayerStore } from '../stores/playerStore'
import { useAudioPlayer } from '../composables/useAudioPlayer'
import { formatTime } from '@/shared/utils/format'
import { onBeforeUnmount, onMounted } from 'vue'
import { isGlobalShortcut } from '@/shared/utils/keyboard'

const audioElement = useTemplateRef<HTMLAudioElement>('audio-element')
useAudioPlayer(audioElement)

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
</script>

<template>
  <!-- TOUJOURS rendu (même sans piste) : c'est l'unique <audio> de l'application -->
  <audio ref="audio-element" preload="metadata" />

  <div v-if="track" class="player" role="region" aria-label="Lecteur audio">
    <button type="button" class="play" :aria-label="isPlaying ? 'Mettre en pause' : 'Lire'"
        aria-keyshortcuts="Space k" :title="isPlaying ? 'Pause (Espace)' : 'Lecture (Espace)'" @click="player.toggle()">
      {{ isPlaying ? '⏸' : '▶' }}
    </button>
    <div class="info">
      <strong>{{ track.title }}</strong>
      <small class="muted">{{ track.subtitle }}</small>
    </div>
    <input
      class="seek" type="range" min="0" step="0.1"
      :max="duration || 0" :value="currentTime" :disabled="!duration"
      aria-label="Position de lecture"
      :aria-valuetext="`${formatTime(currentTime)} sur ${formatTime(duration)}`"
      @input="onSeek"
    />
    <span class="time">{{ formatTime(currentTime) }} / {{ formatTime(duration) }}</span>
    <p v-if="error" class="field-error" role="alert">{{ error }}</p>
  </div>
</template>

<style scoped>
.player { position: fixed; left: 0; right: 0; bottom: 0; display: grid; grid-template-columns: auto minmax(8rem, 1fr) 3fr auto;
          gap: 1rem; align-items: center; padding: .75rem 1.5rem; background: var(--surface); border-top: 1px solid var(--border); }
.play { width: 2.75rem; height: 2.75rem; border-radius: 50%; padding: 0; }
.info { display: grid; overflow: hidden; white-space: nowrap; text-overflow: ellipsis; }
.seek { width: 100%; padding: 0; }
.time { font-variant-numeric: tabular-nums; }
</style>
