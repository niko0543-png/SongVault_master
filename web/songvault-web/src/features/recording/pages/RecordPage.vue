<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { onBeforeRouteLeave, RouterLink, useRoute, useRouter } from 'vue-router'
import { isRecordingSupported, useRecorder, type Recording } from '../composables/useRecorder'
import { formatElapsed, rehearsalTitle } from '../recorder'
import SectionCard from '@/shared/components/SectionCard.vue'
import { songsApi } from '@/features/songs/api/songsApi'
import { useSongsStore } from '@/features/songs/stores/songsStore'
import type { Song } from '@/features/songs/types'
import { versionsApi } from '@/features/versions/api/versionsApi'
import type { SongVersionSummary } from '@/features/versions/types'
import { filesApi } from '@/features/files/api/filesApi'
import { useBandStore } from '@/features/bands/stores/bandStore'
import { useAsyncAction } from '@/shared/composables/useAsyncAction'
import { useConfirm } from '@/shared/composables/useConfirm'
import { ApiError, getErrorMessage } from '@/shared/api/ApiError'
import { formatFileSize } from '@/shared/utils/format'
import { t } from '@/shared/i18n'

/**
 * Enregistrer une répétition (ADR 0049) : le micro du navigateur, puis l'envoi comme n'importe quel fichier audio,
 * dans un nouveau morceau (par défaut), une nouvelle version ou une version existante.
 * ?songId=&versionId= (lien « Enregistrer » d'une version) choisit d'avance la destination.
 */
const route = useRoute()
const router = useRouter()
const bands = useBandStore()
const songsStore = useSongsStore()
const { confirm } = useConfirm()

const supported = isRecordingSupported()
const { state, error, elapsedMs, level, clipped, devices, deviceId, recording, refreshDevices, start, pause, resume, stop, reset } = useRecorder()
const isActive = computed(() => state.value === 'starting' || state.value === 'recording' || state.value === 'paused')

// ================= Destination =================
const NEW = 'new'
const presetSongId = typeof route.query.songId === 'string' ? route.query.songId : ''
const presetVersionId = typeof route.query.versionId === 'string' ? route.query.versionId : ''
const songs = ref<Song[]>([])
const songId = ref(presetSongId)                                // '' : nouveau morceau
const versions = ref<SongVersionSummary[]>([])
const versionId = ref(NEW)
const newTitle = ref('')
const needsTitle = computed(() => !songId.value || versionId.value === NEW)

async function loadSongs() {
  try {
    const all: Song[] = []
    for (let page = 1, pages = 1; page <= pages; page++) {
      const result = await songsApi.list({ page, pageSize: 50 })  // 50 : le maximum de l'API
      all.push(...result.items)
      pages = result.totalPages
    }
    songs.value = all.sort((a, b) => a.title.localeCompare(b.title))
  } catch {
    songs.value = []                                            // reste possible : « Nouveau morceau »
  }
}

watch(songId, async (id) => {
  versions.value = []
  versionId.value = NEW
  if (!id) return
  try {
    const list = await versionsApi.list(id)
    if (songId.value !== id) return                             // autre morceau choisi entre-temps
    versions.value = [...list].sort((a, b) => b.number - a.number)
    if (list.some((version) => version.id === presetVersionId)) versionId.value = presetVersionId
  } catch {
    versions.value = []
  }
}, { immediate: true })

// ================= Écoute avant envoi =================
const objectUrl = ref<string | null>(null)
watch(recording, (next) => {
  if (objectUrl.value) URL.revokeObjectURL(objectUrl.value)
  objectUrl.value = next ? URL.createObjectURL(next.blob) : null
  if (next) newTitle.value = rehearsalTitle(next.startedAt)
})

// ================= Envoi =================
const progress = ref<number | null>(null)
const send = useAsyncAction(async (taken: Recording) => {
  const title = newTitle.value.trim()
  if (!songId.value) {
    const song = await songsApi.create({ title, artist: null, description: null })
    songsStore.upsert(song)
    songs.value.push(song)
    songId.value = song.id                                      // un nouvel essai ne recrée pas le morceau
  }
  const targetSongId = songId.value
  if (versionId.value === NEW) {
    const version = await versionsApi.create(targetSongId, {
      title, status: 'Rehearsal', notes: null, lyrics: null, bpm: null, key: null,
    })
    versions.value.unshift({ id: version.id, number: version.number, title: version.title, status: version.status, createdAt: version.createdAt })
    versionId.value = version.id                                // idem pour la version
  }
  const targetVersionId = versionId.value
  const file = new File([taken.blob], taken.fileName, { type: taken.blob.type })
  await filesApi.upload(targetSongId, targetVersionId, file, (ratio) => { progress.value = ratio })
  return { songId: targetSongId, versionId: targetVersionId }
})
const isQuotaError = computed(() => send.error.value instanceof ApiError && send.error.value.status === 402)
const sendError = computed(() => send.error.value ? getErrorMessage(send.error.value) : null)

async function onSend() {
  if (!recording.value || (needsTitle.value && !newTitle.value.trim())) return
  progress.value = null
  const target = await send.run(recording.value)
  progress.value = null
  if (!target) return
  reset()                                                       // envoyé : plus rien à perdre en quittant
  await router.push({ name: 'version-detail', params: { songId: target.songId, versionId: target.versionId } })
}

async function onDiscard() {
  const ok = await confirm({
    title: t('recording.discardTitle'),
    message: t('recording.discardMessage'),
    confirmLabel: t('recording.discard'),
    danger: true,
  })
  if (ok) reset()
}

// ================= Ne rien perdre en quittant =================
const hasUnsaved = computed(() => isActive.value || recording.value !== null)
onBeforeRouteLeave(async () => {
  if (!hasUnsaved.value) return true
  return confirm({
    title: t('recording.leaveTitle'),
    message: isActive.value ? t('recording.leaveWhileRecording') : t('recording.leaveUnsent'),
    confirmLabel: t('recording.leave'),
    danger: true,
  })
})

function onBeforeUnload(event: BeforeUnloadEvent) {
  if (hasUnsaved.value) event.preventDefault()
}

onMounted(() => {
  window.addEventListener('beforeunload', onBeforeUnload)
  if (supported) void refreshDevices()
  void loadSongs()
})
onBeforeUnmount(() => {
  window.removeEventListener('beforeunload', onBeforeUnload)
  if (objectUrl.value) URL.revokeObjectURL(objectUrl.value)
})
</script>

<template>
  <section>
    <h1>{{ t('recording.title') }}</h1>
    <p class="muted">{{ t('recording.help') }}</p>

    <p v-if="!supported" class="field-error" role="alert" data-testid="recording-unsupported">{{ t('recording.errors.unsupported') }}</p>

    <SectionCard v-else-if="!recording" :title="t('recording.microphone')">
      <p v-if="devices.length > 1" class="field">
        <label for="recording-device">{{ t('recording.device') }}</label>
        <select id="recording-device" v-model="deviceId" :disabled="state !== 'idle'">
          <option value="">{{ t('recording.defaultDevice') }}</option>
          <option v-for="(device, index) in devices" :key="device.deviceId" :value="device.deviceId">
            {{ device.label || t('recording.deviceN', { n: index + 1 }) }}
          </option>
        </select>
      </p>

      <p class="clock" :class="{ 'clock--live': state === 'recording' }" data-testid="recording-clock">
        <span v-if="state === 'recording'" class="dot" aria-hidden="true" />
        {{ formatElapsed(elapsedMs) }}
        <span v-if="state === 'paused'" class="muted paused">{{ t('recording.pausedLabel') }}</span>
      </p>
      <div class="meter" role="meter" :aria-label="t('recording.level')" aria-valuemin="0" aria-valuemax="100"
           :aria-valuenow="Math.round(level * 100)">
        <span class="meter-bar" :class="{ 'meter-bar--clip': clipped }" :style="{ width: `${Math.round(level * 100)}%` }" />
      </div>
      <p v-if="clipped" class="field-error" data-testid="recording-clipping">{{ t('recording.clipping') }}</p>

      <div class="controls">
        <button v-if="state === 'idle' || state === 'starting'" type="button" class="record" :disabled="state === 'starting'"
                data-testid="recording-start" @click="start">
          {{ state === 'starting' ? t('recording.starting') : t('recording.start') }}
        </button>
        <template v-else>
          <button v-if="state === 'recording'" type="button" class="secondary" data-testid="recording-pause" @click="pause">{{ t('recording.pause') }}</button>
          <button v-else type="button" class="secondary" data-testid="recording-resume" @click="resume">{{ t('recording.resume') }}</button>
          <button type="button" data-testid="recording-stop" @click="stop">{{ t('recording.stop') }}</button>
        </template>
      </div>
      <p v-if="isActive" class="muted"><small>{{ t('recording.keepOpen') }}</small></p>
      <p v-if="error" class="field-error" role="alert" data-testid="recording-error">{{ error }}</p>
    </SectionCard>

    <SectionCard v-else :title="t('recording.result')">
      <audio class="preview" controls preload="metadata" :src="objectUrl ?? undefined" />
      <p class="muted" data-testid="recording-summary">
        {{ recording.fileName }} · {{ formatElapsed(recording.durationMs) }} · {{ formatFileSize(recording.blob.size) }}
      </p>
      <p v-if="error" class="field-error" role="alert">{{ error }}</p>

      <form class="destination" @submit.prevent="onSend">
        <fieldset class="bare" :disabled="send.isLoading.value">
          <p class="field">
            <label for="recording-song">{{ t('recording.song') }}</label>
            <select id="recording-song" v-model="songId">
              <option value="">{{ t('recording.newSong') }}</option>
              <option v-for="song in songs" :key="song.id" :value="song.id">{{ song.title }}</option>
            </select>
          </p>
          <p v-if="songId" class="field">
            <label for="recording-version">{{ t('recording.version') }}</label>
            <select id="recording-version" v-model="versionId">
              <option :value="NEW">{{ t('recording.newVersion') }}</option>
              <option v-for="version in versions" :key="version.id" :value="version.id">
                {{ t('recording.versionOption', { number: version.number, title: version.title }) }}
              </option>
            </select>
          </p>
          <p v-if="needsTitle" class="field">
            <label for="recording-title">{{ songId ? t('recording.versionTitle') : t('recording.songTitle') }}</label>
            <input id="recording-title" v-model="newTitle" required maxlength="200" />
          </p>
          <p class="muted"><small>{{ needsTitle ? t('recording.rehearsalStatusHelp') : t('recording.existingVersionHelp') }}</small></p>
        </fieldset>

        <div v-if="send.isLoading.value" role="status">
          <progress :aria-label="t('recording.sending')" max="1" :value="progress ?? undefined" />
          {{ t('recording.sending') }}
          <span v-if="progress !== null">{{ Math.round(progress * 100) }} %</span>
        </div>
        <p v-if="sendError" class="field-error" role="alert" data-testid="recording-send-error">
          {{ sendError }}
          <RouterLink v-if="isQuotaError && bands.can('admin')" :to="{ name: 'billing' }">{{ t('recording.seePlans') }}</RouterLink>
        </p>

        <div class="controls">
          <button type="submit" :disabled="send.isLoading.value || (needsTitle && !newTitle.trim())" data-testid="recording-send">
            {{ t('recording.send') }}
          </button>
          <button type="button" class="secondary" :disabled="send.isLoading.value" @click="onDiscard">{{ t('recording.restart') }}</button>
        </div>
      </form>
    </SectionCard>
  </section>
</template>

<style scoped>
.field { display: flex; flex-direction: column; gap: .25rem; margin: 0 0 .75rem; }
.field select, .field input { max-width: 28rem; }
.clock { font-size: 2.5rem; font-variant-numeric: tabular-nums; font-weight: 600; margin: .5rem 0; display: flex; align-items: center; gap: .6rem; }
.dot { width: .8rem; height: .8rem; border-radius: 50%; background: #d12f2f; animation: blink 1s steps(2, start) infinite; }
.paused { font-size: 1rem; font-weight: 400; }
.meter { height: .75rem; border-radius: var(--radius); background: var(--border); overflow: hidden; max-width: 28rem; }
.meter-bar { display: block; height: 100%; background: #2e9d57; transition: width 80ms linear; }
.meter-bar--clip { background: #d12f2f; }
.controls { display: flex; gap: .75rem; flex-wrap: wrap; margin-top: 1rem; }
button.record { background: #d12f2f; border-color: #d12f2f; }
.preview { width: 100%; max-width: 32rem; }
.destination { margin-top: 1rem; }
.bare { border: 0; padding: 0; margin: 0; min-width: 0; }
@keyframes blink { to { visibility: hidden; } }
@media (prefers-reduced-motion: reduce) { .dot { animation: none; } .meter-bar { transition: none; } }
</style>
