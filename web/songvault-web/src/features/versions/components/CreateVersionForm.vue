<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import StatusSelect from './StatusSelect.vue'
import { versionsApi } from '../api/versionsApi'
import {
  VERSION_NOTES_MAX, VERSION_TITLE_MAX,
  type CreateSongVersionInput, type SongVersion, type SongVersionStatus, type SongVersionSummary,
} from '../types'
import { isReadable } from '@/features/files/types'
import { getErrorMessage } from '@/shared/api/ApiError'
import { formatFileSize } from '@/shared/utils/format'
import { t } from '@/shared/i18n'

/** ADR 0046 : songId et versions permettent de reprendre des éléments d'une version existante. */
const props = withDefaults(defineProps<{
  songId?: string
  versions?: SongVersionSummary[]
  submitting?: boolean
  serverErrors?: Record<string, string>
}>(), {
  songId: undefined,
  versions: () => [],
  submitting: false,
  serverErrors: () => ({}),
})
const emit = defineEmits<{ submit: [input: CreateSongVersionInput] }>()

const form = reactive<{ title: string; status: SongVersionStatus; notes: string }>({ title: '', status: 'Idea', notes: '' })
const submitted = ref(false)

const titleError = computed(() => {
  const title = form.title.trim()
  if (!title) return t('versions.create.titleRequired')
  if (title.length > VERSION_TITLE_MAX) return t('versions.create.maxLength', { versionTitleMax: VERSION_TITLE_MAX })
  return undefined
})

// ---- Reprise (ADR 0046) : par défaut la dernière version, tout sauf les notes ----
const sortedVersions = computed(() => [...props.versions].sort((a, b) => b.number - a.number))
const sourceId = ref(sortedVersions.value[0]?.id ?? '')
const source = ref<SongVersion | null>(null)
const sourceLoading = ref(false)
const sourceError = ref<string | null>(null)
const carry = reactive({ lyrics: true, bpm: true, key: true, notes: false })
const fileIds = ref<string[]>([])

const readableFiles = computed(() => source.value?.files.filter(isReadable) ?? [])
const selectedSize = computed(() =>
  readableFiles.value.filter((f) => fileIds.value.includes(f.id)).reduce((sum, f) => sum + f.sizeBytes, 0))
const hasSomething = computed(() => {
  const s = source.value
  return !!s && (!!s.lyrics || s.bpm !== null || !!s.key || !!s.notes || s.files.length > 0)
})

watch(sourceId, async (id) => {
  source.value = null
  sourceError.value = null
  fileIds.value = []
  if (!id || !props.songId) return
  sourceLoading.value = true
  try {
    const version = await versionsApi.get(props.songId, id)
    if (sourceId.value !== id) return                     // une autre version a été choisie entre-temps
    source.value = version
    fileIds.value = version.files.filter(isReadable).map((f) => f.id)
    if (carry.notes) fillNotes(true)
  } catch (e) {
    if (sourceId.value === id) sourceError.value = getErrorMessage(e)
  } finally {
    if (sourceId.value === id) sourceLoading.value = false
  }
}, { immediate: true })

// Les notes reprises vont dans le champ Notes, où on peut les retoucher ; décocher ne retire que ce qui n'a pas bougé
function fillNotes(on: boolean) {
  const previous = source.value?.notes ?? ''
  if (on && !form.notes.trim()) form.notes = previous
  else if (!on && form.notes === previous) form.notes = ''
}
watch(() => carry.notes, fillNotes)

function selectAllFiles(all: boolean) {
  fileIds.value = all ? readableFiles.value.map((f) => f.id) : []
}

function onSubmit() {
  submitted.value = true
  if (titleError.value || sourceLoading.value) return
  const s = source.value
  const copyFileIds = s ? fileIds.value.filter((id) => readableFiles.value.some((f) => f.id === id)) : []
  emit('submit', {
    title: form.title.trim(),
    status: form.status,
    notes: form.notes.trim() || null,
    lyrics: s && carry.lyrics ? s.lyrics : null,
    bpm: s && carry.bpm ? s.bpm : null,
    key: s && carry.key ? s.key : null,
    copyFromVersionId: copyFileIds.length > 0 ? s!.id : null,
    copyFileIds,
  })
}
</script>

<template>
  <form novalidate class="card" @submit.prevent="onSubmit">
    <h3>{{ t('versions.create.title') }}</h3>
    <div class="field">
      <label for="version-title">{{ t('versions.create.titleLabel') }}</label>
      <input id="version-title" v-model="form.title" :maxlength="VERSION_TITLE_MAX" :placeholder="t('versions.create.titlePlaceholder')" />
      <p v-if="(submitted && titleError) || props.serverErrors.title" class="field-error">
        {{ (submitted && titleError) || props.serverErrors.title }}
      </p>
    </div>
    <div class="field">
      <label for="version-status">{{ t('versions.create.status') }}</label>
      <StatusSelect id="version-status" v-model="form.status" />
    </div>

    <fieldset v-if="songId && sortedVersions.length > 0" class="carry" data-testid="carry-over">
      <legend>{{ t('versions.create.carry.legend') }}</legend>
      <div class="field">
        <label for="version-carry-from">{{ t('versions.create.carry.from') }}</label>
        <select id="version-carry-from" v-model="sourceId">
          <option v-for="v in sortedVersions" :key="v.id" :value="v.id">
            {{ t('versions.create.carry.versionOption', { number: String(v.number), title: v.title }) }}
          </option>
        </select>
      </div>
      <p v-if="sourceLoading" class="muted" role="status">{{ t('versions.create.carry.loading') }}</p>
      <p v-else-if="sourceError" class="field-error" role="alert">{{ sourceError }}</p>
      <template v-else-if="source">
        <p v-if="!hasSomething" class="muted">{{ t('versions.create.carry.nothing') }}</p>
        <label v-if="source.lyrics" class="check"><input v-model="carry.lyrics" type="checkbox" /> {{ t('versions.create.carry.lyrics') }}</label>
        <label v-if="source.bpm !== null" class="check">
          <input v-model="carry.bpm" type="checkbox" /> {{ t('versions.create.carry.bpm', { bpm: String(source.bpm) }) }}
        </label>
        <label v-if="source.key" class="check">
          <input v-model="carry.key" type="checkbox" /> {{ t('versions.create.carry.key', { key: source.key }) }}
        </label>
        <label v-if="source.notes" class="check"><input v-model="carry.notes" type="checkbox" /> {{ t('versions.create.carry.notes') }}</label>

        <div v-if="source.files.length > 0" class="files" role="group" aria-labelledby="version-carry-files">
          <div class="files-head">
            <strong id="version-carry-files">{{ t('versions.create.carry.files') }}</strong>
            <button type="button" class="link" @click="selectAllFiles(true)">{{ t('versions.create.carry.selectAll') }}</button>
            <button type="button" class="link" @click="selectAllFiles(false)">{{ t('versions.create.carry.selectNone') }}</button>
          </div>
          <label v-for="f in source.files" :key="f.id" class="check">
            <input v-model="fileIds" type="checkbox" :value="f.id" :disabled="!isReadable(f)" />
            {{ f.originalFileName }} <small class="muted">· {{ formatFileSize(f.sizeBytes) }}</small>
            <small v-if="!isReadable(f)" class="muted">· {{ t('versions.create.carry.notReadable') }}</small>
          </label>
          <p class="muted" role="status">
            <small>{{ t('versions.create.carry.total', { count: fileIds.length, size: formatFileSize(selectedSize) }) }}</small>
          </p>
          <p class="muted"><small>{{ t('versions.create.carry.filesHelp') }}</small></p>
        </div>
      </template>
    </fieldset>

    <div class="field">
      <label for="version-notes">{{ t('versions.create.notes') }}</label>
      <textarea id="version-notes" v-model="form.notes" rows="3" :maxlength="VERSION_NOTES_MAX" />
    </div>
    <button type="submit" :disabled="submitting || sourceLoading">{{ submitting ? t('versions.create.creating') : t('versions.create.submit') }}</button>
  </form>
</template>

<style scoped>
.carry { border: 1px solid var(--border); border-radius: var(--radius); padding: .5rem .75rem; margin: 0 0 1rem; min-width: 0; }
.check { display: block; margin: .25rem 0; overflow-wrap: anywhere; }
.files { margin-top: .5rem; }
.files-head { display: flex; flex-wrap: wrap; gap: .75rem; align-items: baseline; }
</style>
