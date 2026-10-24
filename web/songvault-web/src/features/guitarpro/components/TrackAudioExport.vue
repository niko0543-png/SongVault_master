<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { filesApi } from '@/features/files/api/filesApi'
import { stemsApi } from '@/features/stems/api/stemsApi'
import { STEM_NAME_MAX, STEMS_MAX, type Stem, type StemInput } from '@/features/stems/types'
import { exportOptions, renderStem, stemFileName, type ExportAudio, type StemSource } from '../export/renderStem'
import { wavBytes } from '../export/wav'
import { SOUND_FONT_URL } from '../soundFont'
import { getErrorMessage } from '@/shared/api/ApiError'
import { useToast } from '@/shared/composables/useToast'
import { formatFileSize, formatTime } from '@/shared/utils/format'
import { t } from '@/shared/i18n'

/**
 * #49 : crée les pistes audio d'une version depuis son fichier Guitar Pro (ADR 0049). Chaque piste cochée est rendue
 * en WAV par le synthétiseur d'alphaTab, envoyée comme un fichier de la version, puis ajoutée aux pistes séparées
 * (ADR 0030) après celles qui y sont déjà : le mélangeur de la version les joue ensemble.
 */
const props = defineProps<{
  songId: string; versionId: string; fileName: string
  tracks: readonly { name: string }[]; exportAudio: ExportAudio
}>()
const emit = defineEmits<{ close: [] }>()
const toast = useToast()

interface Row { source: StemSource; name: string; checked: boolean }

const status = ref<'loading' | 'ready' | 'working' | 'done' | 'error'>('loading')
const loadError = ref<string | null>(null)
const existing = ref<Stem[]>([])
const rows = ref<Row[]>([])
const durationMs = ref(0)
let soundFont: Uint8Array | null = null

const slots = computed(() => Math.max(0, STEMS_MAX - existing.value.length))
const checked = computed(() => rows.value.filter((r) => r.checked))
const totalBytes = computed(() => checked.value.length * wavBytes(durationMs.value))
const problem = computed(() => {
  if (checked.value.length === 0) return t('guitarpro.export.noneChecked')
  if (checked.value.length > slots.value) return t('guitarpro.export.tooMany', { count: slots.value, slots: slots.value, stemsMax: STEMS_MAX })
  if (checked.value.some((r) => r.name.trim() === '')) return t('guitarpro.export.nameRequired')
  return null
})

// Pendant le travail : la piste en cours, et où elle en est
const current = ref<{ index: number; name: string; step: 'render' | 'upload'; percent: number } | null>(null)
const created = ref<StemInput[]>([])
const skipped = ref<string[]>([])
const workError = ref<string | null>(null)
let abort: AbortController | null = null

onMounted(async () => {
  try {
    const [stems, response] = await Promise.all([stemsApi.list(props.songId, props.versionId), fetch(SOUND_FONT_URL)])
    if (!response.ok) {
      loadError.value = t('guitarpro.export.soundFontFailed')
      status.value = 'error'
      return
    }
    soundFont = new Uint8Array(await response.arrayBuffer())
    existing.value = stems
    rows.value = [
      ...props.tracks.map((track, index): Row => ({
        source: { kind: 'track', index },
        name: (track.name.trim() || t('guitarpro.export.trackFallback', { number: index + 1 })).slice(0, STEM_NAME_MAX),
        checked: index < slots.value,
      })),
      { source: { kind: 'click' }, name: t('guitarpro.export.click'), checked: false },
    ]
    durationMs.value = await probeDuration(soundFont)
    status.value = 'ready'
  } catch (e) {
    loadError.value = getErrorMessage(e)
    status.value = 'error'
  }
})
onBeforeUnmount(() => abort?.abort())

/** La durée du morceau, que le premier morceau de rendu annonce ; rien n'est gardé. */
async function probeDuration(bank: Uint8Array) {
  const exporter = await props.exportAudio(exportOptions({ kind: 'click' }, props.tracks.length, bank))
  try {
    return (await exporter.render(10))?.endTime ?? 0
  } finally {
    exporter.destroy()
  }
}

async function onCreate() {
  if (problem.value || !soundFont) return
  status.value = 'working'
  abort = new AbortController()
  const signal = abort.signal
  created.value = []
  skipped.value = []
  workError.value = null
  const todo = checked.value.map((r) => ({ source: r.source, name: r.name.trim() }))
  try {
    for (const [index, row] of todo.entries()) {
      current.value = { index: index + 1, name: row.name, step: 'render', percent: 0 }
      const stem = await renderStem(props.exportAudio, exportOptions(row.source, props.tracks.length, soundFont), {
        signal,
        onProgress: (ratio) => { current.value = { ...current.value!, percent: Math.round(ratio * 100) } },
      })
      if (stem.silent) {                                       // un fichier muet ne servirait à rien dans le mélangeur
        skipped.value = [...skipped.value, row.name]
        continue
      }
      signal.throwIfAborted()
      current.value = { ...current.value, step: 'upload', percent: 0 }
      const file = new File([stem.wav], stemFileName(props.fileName, row.name), { type: 'audio/wav' })
      const uploaded = await filesApi.upload(props.songId, props.versionId, file, (ratio) => {
        current.value = { ...current.value!, percent: Math.round(ratio * 100) }
      })
      created.value = [...created.value, { fileId: uploaded.id, name: row.name }]
    }
  } catch (e) {
    workError.value = signal.aborted ? t('guitarpro.export.cancelled') : getErrorMessage(e)
  }
  current.value = null
  abort = null
  // Les fichiers déjà envoyés deviennent des pistes même après une erreur : ils sont dans la version de toute façon
  if (created.value.length > 0) {
    try {
      const input = [...existing.value.map((s) => ({ fileId: s.fileId, name: s.name })), ...created.value]
      existing.value = await stemsApi.save(props.songId, props.versionId, input)
      if (!workError.value) toast.success(t('guitarpro.export.done', { count: created.value.length }))
    } catch (e) {
      workError.value = getErrorMessage(e)
    }
  }
  status.value = 'done'
}
</script>

<template>
  <section class="card track-export" aria-labelledby="track-export-title" data-testid="track-export">
    <header class="head">
      <h2 id="track-export-title">{{ t('guitarpro.export.title') }}</h2>
      <button type="button" class="secondary" :disabled="status === 'working'" data-testid="track-export-close"
              @click="emit('close')">
        {{ t('guitarpro.export.close') }}
      </button>
    </header>

    <p v-if="status === 'loading'" class="muted" role="status">{{ t('guitarpro.export.loading') }}</p>
    <p v-else-if="status === 'error'" class="field-error" role="alert">{{ loadError }}</p>

    <form v-else-if="status === 'ready'" @submit.prevent="onCreate">
      <p class="muted">{{ t('guitarpro.export.help') }}</p>
      <ul class="rows">
        <li v-for="(row, i) in rows" :key="i" class="row" data-testid="track-export-row">
          <input v-model="row.checked" type="checkbox"
                 :aria-label="t('guitarpro.export.include', { name: row.name || String(i + 1) })" />
          <input v-model="row.name" :maxlength="STEM_NAME_MAX" :disabled="!row.checked"
                 :aria-label="t('guitarpro.export.nameOf', { number: i + 1 })" />
          <small v-if="row.source.kind === 'click'" class="muted">{{ t('guitarpro.export.clickHint') }}</small>
        </li>
      </ul>
      <p class="muted" data-testid="track-export-size">
        {{ t('guitarpro.export.size', { duration: formatTime(durationMs / 1000), each: formatFileSize(wavBytes(durationMs)), total: formatFileSize(totalBytes) }) }}
      </p>
      <p v-if="existing.length > 0" class="muted">
        {{ t('guitarpro.export.existing', { count: existing.length, slots }) }}
      </p>
      <p v-if="problem" class="field-error" role="alert">{{ problem }}</p>
      <div class="actions">
        <button type="submit" :disabled="!!problem" data-testid="track-export-create">
          {{ t('guitarpro.export.create', { count: checked.length }) }}
        </button>
      </div>
    </form>

    <div v-else-if="status === 'working'" class="working">
      <p v-if="current" role="status" data-testid="track-export-progress">
        {{ t(current.step === 'render' ? 'guitarpro.export.rendering' : 'guitarpro.export.uploading',
             { index: current.index, count: checked.length, name: current.name, percent: current.percent }) }}
      </p>
      <progress v-if="current" max="100" :value="current.percent" />
      <button type="button" class="secondary" data-testid="track-export-cancel" @click="abort?.abort()">
        {{ t('common.actions.cancel') }}
      </button>
    </div>

    <div v-else class="done" data-testid="track-export-done">
      <p v-if="created.length > 0">{{ t('guitarpro.export.created', { count: created.length, names: created.map((c) => c.name).join(', ') }) }}</p>
      <p v-if="skipped.length > 0" class="muted">{{ t('guitarpro.export.skipped', { names: skipped.join(', ') }) }}</p>
      <p v-if="workError" class="field-error" role="alert">{{ workError }}</p>
      <p v-if="created.length > 0" class="muted">{{ t('guitarpro.export.preparing') }}</p>
      <RouterLink :to="{ name: 'version-detail', params: { songId, versionId } }" data-testid="track-export-version">
        {{ t('guitarpro.export.toVersion') }}
      </RouterLink>
    </div>
  </section>
</template>

<style scoped>
.track-export { flex: none; max-height: 50vh; overflow: auto; }
.head { display: flex; align-items: center; justify-content: space-between; gap: 1rem; }
.head h2 { margin: 0; font-size: 1rem; }
.rows { list-style: none; margin: .5rem 0; padding: 0; display: grid; gap: .25rem; }
.row { display: flex; align-items: center; gap: .5rem; }
.row input:not([type='checkbox']) { flex: 0 1 16rem; min-width: 0; }
.actions { display: flex; justify-content: flex-end; }
.working { display: grid; gap: .5rem; justify-items: start; }
.working progress { width: 100%; }
</style>
