<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, useId } from 'vue'
import { RouterLink } from 'vue-router'
import { filesApi } from '../api/filesApi'
import type { AudioSplit, AudioWaveform } from '../types'
import { WAVEFORM_HEIGHT, waveformBars, waveformPath } from '../waveform'
import {
  cutAt, formatCutTime, keptSegments, mergeWithNext, moveCut, splitProblem, SPLIT_TITLE_MAX, toSplitRequest, wholeFile,
  type SplitSegment,
} from '../split'
import { versionsApi } from '@/features/versions/api/versionsApi'
import { songsApi } from '@/features/songs/api/songsApi'
import type { Song } from '@/features/songs/types'
import { useBandStore } from '@/features/bands/stores/bandStore'
import LoadingState from '@/shared/components/LoadingState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import SectionCard from '@/shared/components/SectionCard.vue'
import { getErrorMessage } from '@/shared/api/ApiError'
import { t } from '@/shared/i18n'

/** ADR 0050 : un enregistrement d'une répétition entière, découpé en un fichier par morceau joué. */
const BARS = 600
const KEY_STEP_MS = 5000
const POLL_MS = 3000
const SONG_PAGES = 20                    // 20 × 50 morceaux : tous ceux d'un groupe, en pratique

const props = defineProps<{ songId: string; versionId: string; fileId: string }>()
const bands = useBandStore()
const canWrite = computed(() => bands.can('write'))

const fileName = ref('')
const versionTitle = ref('')
const waveform = ref<AudioWaveform | null>(null)
const songs = ref<Song[]>([])
const segments = ref<SplitSegment[]>([])
const latest = ref<AudioSplit | null>(null)
const isLoading = ref(true)
const error = ref<unknown>(null)
const submitError = ref<string | null>(null)
const isSubmitting = ref(false)
const audio = ref<HTMLAudioElement | null>(null)
const positionMs = ref(0)
const isPlaying = ref(false)
const clipId = `kept-${useId()}`
let pollTimer: ReturnType<typeof setTimeout> | null = null

const durationMs = computed(() => waveform.value?.durationMs ?? 0)
const bars = computed(() => waveformBars(waveform.value?.peaks ?? [], BARS))
const path = computed(() => waveformPath(bars.value))
const kept = computed(() => keptSegments(segments.value))
const problem = computed(() => splitProblem(segments.value))
const isRunning = computed(() => latest.value?.status === 'Pending')
const listenUrl = computed(() => filesApi.listenUrl(props.songId, props.versionId, props.fileId))
const songTitles = computed(() => new Map(songs.value.map((s) => [s.id, s.title])))
/** « Morceau 3 » : un nom donné d'office, que le choix d'un morceau remplace. */
const defaultTitlePattern = computed(() => new RegExp(`^${t('files.split.defaultTitle', { n: 0 }).replace('0', '\\d+')}$`))

/** Position dans le dessin (une unité par barre). */
const x = (ms: number) => (durationMs.value > 0 ? (ms / durationMs.value) * bars.value.length : 0)

async function loadSongs() {
  const all: Song[] = []
  for (let page = 1; page <= SONG_PAGES; page++) {
    const result = await songsApi.list({ page, pageSize: 50 })
    all.push(...result.items)
    if (all.length >= result.totalCount || result.items.length === 0) break
  }
  songs.value = all.sort((a, b) => a.title.localeCompare(b.title))
}

async function load() {
  isLoading.value = true
  error.value = null
  try {
    const [version, wave, split] = await Promise.all([
      versionsApi.get(props.songId, props.versionId),
      filesApi.waveform(props.songId, props.versionId, props.fileId),
      filesApi.latestSplit(props.songId, props.versionId, props.fileId),
      loadSongs(),
    ])
    fileName.value = version.files.find((f) => f.id === props.fileId)?.originalFileName ?? ''
    versionTitle.value = `v${version.number} — ${version.title}`
    waveform.value = wave
    segments.value = wholeFile(wave.durationMs, t('files.split.defaultTitle', { n: 1 }))
    latest.value = split
    schedulePoll()
  } catch (e) {
    error.value = e
  } finally {
    isLoading.value = false
  }
}

// ---------- Écoute ----------
function togglePlay() {
  const el = audio.value
  if (!el) return
  if (el.paused) void el.play()
  else el.pause()
}

function seek(ms: number) {
  const clamped = Math.min(Math.max(ms, 0), durationMs.value)
  positionMs.value = clamped
  if (audio.value) audio.value.currentTime = clamped / 1000
}

function playFrom(segment: SplitSegment) {
  seek(segment.startMs)
  void audio.value?.play()
}

function onTimeUpdate() {
  positionMs.value = Math.round((audio.value?.currentTime ?? 0) * 1000)
}

function onWaveformClick(event: MouseEvent) {
  const box = (event.currentTarget as HTMLElement).getBoundingClientRect()
  if (box.width > 0) seek(((event.clientX - box.left) / box.width) * durationMs.value)
}

function onWaveformKey(event: KeyboardEvent) {
  const actions: Record<string, () => void> = {
    ArrowLeft: () => seek(positionMs.value - KEY_STEP_MS),
    ArrowRight: () => seek(positionMs.value + KEY_STEP_MS),
    Home: () => seek(0),
    End: () => seek(durationMs.value),
    ' ': togglePlay,
    c: cutHere,
  }
  const action = actions[event.key]
  if (!action) return
  event.preventDefault()
  action()
}

// ---------- Coupures ----------
function cutHere() {
  const before = segments.value.length
  segments.value = cutAt(segments.value, positionMs.value)
  const added = segments.value.find((s) => s.startMs === Math.round(positionMs.value))
  if (segments.value.length > before && added) added.title = t('files.split.defaultTitle', { n: segments.value.indexOf(added) + 1 })
}

function merge(index: number) {
  segments.value = mergeWithNext(segments.value, index)
}

/** Recule ou avance une coupure d'une seconde (affiner à l'oreille). */
function nudge(index: number, deltaMs: number) {
  const next = segments.value[index + 1]
  if (next) segments.value = moveCut(segments.value, index, next.startMs + deltaMs)
}

function chooseSong(segment: SplitSegment, songId: string) {
  const previous = segment.songId ? songTitles.value.get(segment.songId) : null
  segment.songId = songId || null
  // Le nom suit le morceau choisi, sauf s'il a été tapé à la main
  const isDefault = segment.title.trim() === '' || segment.title === previous || defaultTitlePattern.value.test(segment.title)
  if (segment.songId && isDefault) segment.title = songTitles.value.get(segment.songId) ?? segment.title
}

// ---------- Envoi et suivi ----------
async function submit() {
  if (problem.value) return
  submitError.value = null
  isSubmitting.value = true
  try {
    latest.value = await filesApi.split(props.songId, props.versionId, props.fileId, toSplitRequest(segments.value))
    schedulePoll()
  } catch (e) {
    submitError.value = getErrorMessage(e)
  } finally {
    isSubmitting.value = false
  }
}

function schedulePoll() {
  if (pollTimer !== null || !isRunning.value) return
  pollTimer = setTimeout(async () => {
    pollTimer = null
    try {
      latest.value = await filesApi.latestSplit(props.songId, props.versionId, props.fileId)
    } catch {
      // erreur passagère : on réessaiera au prochain tour
    }
    schedulePoll()
  }, POLL_MS)
}

onMounted(load)
onBeforeUnmount(() => {
  if (pollTimer !== null) clearTimeout(pollTimer)
  audio.value?.pause()
})
</script>

<template>
  <section>
    <RouterLink :to="{ name: 'version-detail', params: { songId, versionId } }">← {{ versionTitle || t('files.split.back') }}</RouterLink>
    <h1>{{ t('files.split.title', { fileName }) }}</h1>

    <LoadingState v-if="isLoading" />
    <ErrorState v-else-if="error" :message="getErrorMessage(error)" @retry="load()" />

    <template v-else-if="waveform">
      <SectionCard v-if="latest && latest.status !== 'Failed'" :title="t('files.split.resultTitle')" data-testid="split-result">
        <p v-if="isRunning" role="status">{{ t('files.split.running', { count: latest.parts.length }) }}</p>
        <template v-else>
          <p role="status">{{ t('files.split.done', { count: latest.parts.length }) }}</p>
          <ul class="results">
            <li v-for="part in latest.parts" :key="part.startMs">
              <RouterLink v-if="part.createdSongId && part.createdVersionId"
                          :to="{ name: 'version-detail', params: { songId: part.createdSongId, versionId: part.createdVersionId } }">
                {{ part.title }}
              </RouterLink>
              <small class="muted"> {{ formatCutTime(part.startMs) }} → {{ formatCutTime(part.endMs) }}</small>
            </li>
          </ul>
          <button v-if="canWrite" type="button" class="secondary" @click="latest = null">{{ t('files.split.again') }}</button>
        </template>
      </SectionCard>

      <template v-if="!latest || latest.status === 'Failed'">
        <p v-if="latest?.status === 'Failed'" class="error" role="alert" data-testid="split-failed">
          {{ t('files.split.failed', { error: latest.error ?? '' }) }}
        </p>
        <p class="muted">{{ t('files.split.help') }}</p>

        <div class="card player">
          <audio ref="audio" :src="listenUrl" preload="metadata"
                 @timeupdate="onTimeUpdate" @play="isPlaying = true" @pause="isPlaying = false" />
          <div class="controls">
            <button type="button" class="secondary" @click="togglePlay">{{ isPlaying ? t('files.split.pause') : t('files.split.play') }}</button>
            <span class="time">{{ formatCutTime(positionMs) }} / {{ formatCutTime(durationMs) }}</span>
            <button v-if="canWrite" type="button" data-testid="cut-here" @click="cutHere">{{ t('files.split.cutHere') }}</button>
          </div>
          <div class="wave" role="slider" tabindex="0" data-testid="split-waveform"
               :aria-label="t('files.split.waveformLabel')" aria-valuemin="0" :aria-valuemax="Math.round(durationMs / 1000)"
               :aria-valuenow="Math.round(positionMs / 1000)" :aria-valuetext="formatCutTime(positionMs)"
               @click="onWaveformClick" @keydown="onWaveformKey">
            <svg :viewBox="`0 0 ${bars.length} ${WAVEFORM_HEIGHT}`" preserveAspectRatio="none" aria-hidden="true">
              <defs>
                <clipPath :id="clipId">
                  <rect v-for="s in kept" :key="s.startMs" :x="x(s.startMs)" :width="x(s.endMs) - x(s.startMs)" :height="WAVEFORM_HEIGHT" />
                </clipPath>
              </defs>
              <path class="dropped" :d="path" />
              <path class="kept" :d="path" :clip-path="`url(#${clipId})`" />
              <line v-for="s in segments.slice(1)" :key="s.startMs" class="cut" :x1="x(s.startMs)" :x2="x(s.startMs)" y1="0" :y2="WAVEFORM_HEIGHT" />
              <line class="head" :x1="x(positionMs)" :x2="x(positionMs)" y1="0" :y2="WAVEFORM_HEIGHT" />
            </svg>
          </div>
          <small class="muted">{{ t('files.split.keys') }}</small>
        </div>

        <ol class="segments">
          <li v-for="(segment, index) in segments" :key="segment.startMs" class="card segment" :class="{ off: !segment.keep }"
              data-testid="split-segment">
            <div class="segment-head">
              <label class="keep">
                <input v-model="segment.keep" type="checkbox" :disabled="!canWrite" />
                {{ t('files.split.keep') }}
              </label>
              <span class="range">{{ formatCutTime(segment.startMs) }} → {{ formatCutTime(segment.endMs) }}</span>
              <button type="button" class="link" @click="playFrom(segment)">{{ t('files.split.listen') }}</button>
            </div>
            <div v-if="segment.keep" class="segment-fields">
              <label>
                {{ t('files.split.destination') }}
                <select :value="segment.songId ?? ''" :disabled="!canWrite" data-testid="split-destination"
                        @change="chooseSong(segment, ($event.target as HTMLSelectElement).value)">
                  <option value="">{{ t('files.split.newSong') }}</option>
                  <option v-for="song in songs" :key="song.id" :value="song.id">{{ t('files.split.newVersionOf', { title: song.title }) }}</option>
                </select>
              </label>
              <label>
                {{ segment.songId ? t('files.split.partName') : t('files.split.songTitle') }}
                <input v-model="segment.title" type="text" :maxlength="SPLIT_TITLE_MAX" required :disabled="!canWrite"
                       data-testid="split-title" />
              </label>
            </div>
            <div v-if="canWrite && index < segments.length - 1" class="cut-actions">
              <small class="muted">{{ t('files.split.cutAt', { time: formatCutTime(segment.endMs) }) }}</small>
              <button type="button" class="link" :aria-label="t('files.split.earlierLabel')" @click="nudge(index, -1000)">{{ t('files.split.earlier') }}</button>
              <button type="button" class="link" :aria-label="t('files.split.laterLabel')" @click="nudge(index, 1000)">{{ t('files.split.later') }}</button>
              <button type="button" class="link" data-testid="split-merge" @click="merge(index)">{{ t('files.split.removeCut') }}</button>
            </div>
          </li>
        </ol>

        <div v-if="canWrite" class="submit">
          <p v-if="problem" class="muted">{{ t(`files.split.problem.${problem}`) }}</p>
          <p v-if="submitError" class="error" role="alert">{{ submitError }}</p>
          <button type="button" :disabled="!!problem || isSubmitting" data-testid="split-submit" @click="submit">
            {{ t('files.split.submit', { count: kept.length }) }}
          </button>
        </div>
      </template>
    </template>
  </section>
</template>

<style scoped>
.player { display: grid; gap: .5rem; margin-bottom: 1rem; }
.controls { display: flex; gap: .75rem; align-items: center; flex-wrap: wrap; }
.time { font-variant-numeric: tabular-nums; }
.wave { cursor: pointer; }
.wave:focus-visible { outline: 2px solid var(--primary); outline-offset: 2px; border-radius: var(--radius); }
svg { width: 100%; height: 6rem; display: block; }
.dropped { fill: var(--border); }
.kept { fill: var(--primary); }
.cut { stroke: var(--danger); stroke-width: .6; vector-effect: non-scaling-stroke; }
.head { stroke: currentColor; stroke-width: 1; vector-effect: non-scaling-stroke; }
.segments { list-style: none; padding: 0; display: grid; gap: .5rem; }
.segment { display: grid; gap: .5rem; }
.segment.off { opacity: .6; }
.keep { display: inline-flex; gap: .35rem; align-items: center; margin: 0; }
.segment-head, .cut-actions { display: flex; gap: .75rem; align-items: center; flex-wrap: wrap; }
.range { font-variant-numeric: tabular-nums; }
.segment-fields { display: grid; grid-template-columns: repeat(auto-fit, minmax(14rem, 1fr)); gap: .5rem; }
.segment-fields label { display: grid; gap: .25rem; }
.results { padding-left: 1.25rem; }
.submit { display: grid; gap: .5rem; justify-items: start; }
.error { color: var(--danger); }
.link { background: none; border: 0; padding: 0; color: var(--primary); text-decoration: underline; cursor: pointer; font: inherit; }
</style>
