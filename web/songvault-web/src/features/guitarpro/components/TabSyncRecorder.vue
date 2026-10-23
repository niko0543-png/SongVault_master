<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { usePlayerStore } from '@/features/player/stores/playerStore'
import type { SyncAudio } from '../sync/types'
import { t } from '@/shared/i18n'

/**
 * #29 : calage « au tapé » (ADR 0036). La maquette joue ici, dans son propre élément <audio> ; à chaque début de
 * mesure, on appuie sur le grand bouton. La tape n° i marque la i-ième mesure jouée. On peut s'arrêter avant la fin :
 * alphaTab estime la suite à partir du dernier point.
 */
const props = defineProps<{
  audios: SyncAudio[]
  /** Maquette proposée d'abord : celle du calage actuel, sinon la première. */
  initialAudioId?: string
  /** L'adresse d'écoute d'un fichier audio (…/files/{id}/listen). */
  src: (audioFileId: string) => string
  /** Nombre de mesures jouées, reprises comprises. */
  bars: number
  saving?: boolean
}>()
const emit = defineEmits<{
  /** La mesure jouée (dans l'ordre de lecture, à partir de 0) que la prochaine tape marquera. */
  next: [index: number]
  save: [audioFileId: string, tapsMs: number[]]
  cancel: []
}>()
const player = usePlayerStore()

const audio = ref<HTMLAudioElement | null>(null)
const audioId = ref(props.initialAudioId && props.audios.some((a) => a.id === props.initialAudioId)
  ? props.initialAudioId : props.audios[0]?.id ?? '')
const playing = ref(false)
const taps = ref<number[]>([])
const tapButton = ref<HTMLButtonElement | null>(null)

const done = computed(() => taps.value.length >= props.bars)
const nextBar = computed(() => Math.min(taps.value.length + 1, props.bars))

watch(audioId, () => {                                       // autre maquette : on recommence
  audio.value?.pause()
  taps.value = []
})
watch(() => taps.value.length, (count) => emit('next', Math.min(count, props.bars - 1)), { immediate: true })
// Un seul son à la fois : le lecteur du bas repart, la maquette se tait
watch(() => player.isPlaying, (value) => { if (value) audio.value?.pause() })

onBeforeUnmount(() => audio.value?.pause())

async function playPause() {
  const element = audio.value
  if (!element) return
  if (!element.paused) return element.pause()
  if (taps.value.length === 0) element.currentTime = 0       // un calage part du début de la maquette
  player.pause()
  try {
    await element.play()
    tapButton.value?.focus()                                 // Espace ou Entrée tapent alors la mesure
  } catch {
    playing.value = false
  }
}

function tap() {
  const element = audio.value
  if (!element || element.paused || done.value) return
  const ms = Math.round(element.currentTime * 1000)
  if (ms > (taps.value[taps.value.length - 1] ?? -1)) taps.value = [...taps.value, ms]
  if (done.value) element.pause()
}

function undo() {
  taps.value = taps.value.slice(0, -1)
}

function restart() {
  audio.value?.pause()
  taps.value = []
}
</script>

<template>
  <section class="recorder" :aria-label="t('guitarpro.sync.recorderLabel')" data-testid="sync-recorder">
    <label class="inline">
      {{ t('guitarpro.sync.audio') }}
      <select v-model="audioId" data-testid="sync-audio-choice">
        <option v-for="a in audios" :key="a.id" :value="a.id">{{ a.name }}</option>
      </select>
    </label>
    <p class="muted"><small>{{ t('guitarpro.sync.howTo') }}</small></p>

    <!-- La maquette du calage : pas de contrôles natifs, les boutons ci-dessous suffisent -->
    <audio ref="audio" :src="src(audioId)" preload="auto" data-testid="sync-audio"
           @play="playing = true" @pause="playing = false" @ended="playing = false" />

    <div class="actions">
      <button type="button" class="secondary" data-testid="sync-play" @click="playPause">
        {{ playing ? t('guitarpro.sync.pauseAudio') : t('guitarpro.sync.playAudio') }}
      </button>
      <button ref="tapButton" type="button" class="tap" data-testid="sync-tap" :disabled="!playing || done" @click="tap">
        {{ done ? t('guitarpro.sync.allTapped') : t('guitarpro.sync.tap', { bar: nextBar, total: bars }) }}
      </button>
      <button type="button" class="secondary" :disabled="taps.length === 0" @click="undo">{{ t('guitarpro.sync.undo') }}</button>
      <button type="button" class="secondary" :disabled="taps.length === 0" @click="restart">{{ t('guitarpro.sync.restart') }}</button>
    </div>
    <p role="status" data-testid="sync-count">{{ t('guitarpro.sync.count', { count: taps.length, total: bars }) }}</p>

    <div class="actions">
      <button type="button" data-testid="sync-save" :disabled="taps.length === 0 || saving" @click="emit('save', audioId, taps)">
        {{ t('guitarpro.sync.save') }}
      </button>
      <button type="button" class="secondary" :disabled="saving" @click="emit('cancel')">{{ t('common.actions.cancel') }}</button>
    </div>
  </section>
</template>

<style scoped>
.recorder { display: grid; gap: .5rem; padding: .75rem; border: 1px solid var(--border); border-radius: var(--radius); }
.recorder p { margin: 0; }
.inline { display: inline-flex; gap: .35rem; align-items: center; width: auto; }
.inline select { width: auto; max-width: 100%; }
.actions { display: flex; flex-wrap: wrap; gap: .5rem; align-items: center; }
/* Le bouton que l'on tape en rythme : grand, pour le doigt comme pour la souris */
.tap { min-height: 56px; min-width: 12rem; font-size: 1.05rem; }
</style>