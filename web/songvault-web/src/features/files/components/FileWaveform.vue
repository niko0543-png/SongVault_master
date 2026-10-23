<script setup lang="ts">
import { computed, onMounted, ref, useId } from 'vue'
import { filesApi } from '../api/filesApi'
import type { AudioWaveform } from '../types'
import { WAVEFORM_HEIGHT, waveformBars, waveformPath } from '../waveform'
import { formatTime } from '@/shared/utils/format'
import { t } from '@/shared/i18n'

const BARS = 200
const KEY_STEP_SECONDS = 5

const props = defineProps<{ songId: string; versionId: string; fileId: string; label: string; progress?: number }>()
const emit = defineEmits<{ seek: [seconds: number] }>()

const waveform = ref<AudioWaveform | null>(null)
const clipId = `played-${useId()}`

onMounted(async () => {
  try {
    waveform.value = await filesApi.waveform(props.songId, props.versionId, props.fileId)
  } catch {
    waveform.value = null          // la forme d'onde est un confort : sans elle, le bouton Écouter suffit
  }
})

const bars = computed(() => waveformBars(waveform.value?.peaks ?? [], BARS))
const path = computed(() => waveformPath(bars.value))
const duration = computed(() => (waveform.value?.durationMs ?? 0) / 1000)
const ratio = computed(() => Math.min(Math.max(props.progress ?? 0, 0), 1))
const position = computed(() => ratio.value * duration.value)

function seekTo(seconds: number) {
  emit('seek', Math.min(Math.max(seconds, 0), duration.value))
}

function onClick(event: MouseEvent) {
  const box = (event.currentTarget as HTMLElement).getBoundingClientRect()
  if (box.width > 0) seekTo(((event.clientX - box.left) / box.width) * duration.value)
}

function onKey(event: KeyboardEvent) {
  const target = {
    ArrowLeft: position.value - KEY_STEP_SECONDS,
    ArrowRight: position.value + KEY_STEP_SECONDS,
    Home: 0,
    End: duration.value,
  }[event.key]
  if (target === undefined) return
  event.preventDefault()
  seekTo(target)
}
</script>

<template>
  <div v-if="waveform" class="waveform" role="slider" tabindex="0" data-testid="waveform"
       :aria-label="t('files.waveform.position', { label })" aria-valuemin="0" :aria-valuemax="Math.round(duration)"
       :aria-valuenow="Math.round(position)"
       :aria-valuetext="t('shared.time.positionOf', { position: formatTime(position), duration: formatTime(duration) })"
       @click="onClick" @keydown="onKey">
    <svg :viewBox="`0 0 ${bars.length} ${WAVEFORM_HEIGHT}`" preserveAspectRatio="none" aria-hidden="true">
      <defs>
        <clipPath :id="clipId"><rect :width="ratio * bars.length" :height="WAVEFORM_HEIGHT" /></clipPath>
      </defs>
      <path class="rest" :d="path" />
      <path class="played" :d="path" :clip-path="`url(#${clipId})`" />
    </svg>
    <small class="muted">{{ formatTime(duration) }}</small>
  </div>
</template>

<style scoped>
.waveform { display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: .5rem; align-items: center; cursor: pointer; }
.waveform:focus-visible { outline: 2px solid var(--primary); outline-offset: 2px; border-radius: var(--radius); }
svg { width: 100%; height: 2.5rem; display: block; }
.rest { fill: var(--border); }
.played { fill: var(--primary); }
</style>