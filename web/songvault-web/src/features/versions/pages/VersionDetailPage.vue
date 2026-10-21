<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { onBeforeRouteLeave, onBeforeRouteUpdate, RouterLink, useRouter } from 'vue-router'
import { useVersion } from '../composables/useVersion'
import StatusBadge from '../components/StatusBadge.vue'
import StatusSelect from '../components/StatusSelect.vue'
import {
  MUSICAL_KEY_MAX, MUSICAL_KEY_PATTERN,
  VERSION_BPM_MAX, VERSION_BPM_MIN,
  VERSION_NOTES_MAX, VERSION_TITLE_MAX,
  type SongVersionStatus,
} from '../types'
import FileList from '@/features/files/components/FileList.vue'
import FileUploader from '@/features/files/components/FileUploader.vue'
import type { SongFile } from '@/features/files/types'
import { filesApi } from '@/features/files/api/filesApi'
import { usePreviewPolling } from '@/features/files/composables/usePreviewPolling'
import { versionsApi } from '../api/versionsApi'
import CommentsPanel from '@/features/comments/components/CommentsPanel.vue'
import ShareLinksPanel from '@/features/sharing/components/ShareLinksPanel.vue'
import StemsSection from '@/features/stems/components/StemsSection.vue'
import LyricsPanel from '@/features/chords/components/LyricsPanel.vue'
import { usePlayerStore } from '@/features/player/stores/playerStore'
import { toTrack, type Track } from '@/features/player/track'
import { useBandStore } from '@/features/bands/stores/bandStore'
import SectionCard from '@/shared/components/SectionCard.vue'
import LoadingState from '@/shared/components/LoadingState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import { ApiError, getErrorMessage } from '@/shared/api/ApiError'
import { mapProblemErrors } from '@/shared/api/mapProblemErrors'
import { formatDate } from '@/shared/utils/format'
import { useConfirm } from '@/shared/composables/useConfirm'
import { useToast } from '@/shared/composables/useToast'
import { t } from '@/shared/i18n'

const props = defineProps<{ songId: string; versionId: string }>()

const { song, version, previous, next, isLoading, error, isSaving, saveError, save, reload } =
  useVersion(() => props.songId, () => props.versionId)
const player = usePlayerStore()
const bands = useBandStore()
const canWrite = computed(() => bands.can('write'))
const isNotFound = computed(() => error.value instanceof ApiError && error.value.status === 404)
const { confirm } = useConfirm()
const toast = useToast()
const router = useRouter()
// ================= Formulaire =================
interface VersionForm {
  title: string
  status: SongVersionStatus
  notes: string
  lyrics: string
  bpm: number | ''     // <input type="number" v-model> : Vue convertit en nombre ; '' quand le champ est vide
  key: string
}

const form = reactive<VersionForm>({ title: '', status: 'Idea', notes: '', lyrics: '', bpm: '', key: '' })

/** Recopie la version chargée dans le formulaire (chargement, enregistrement, annulation). */
function resetForm() {
  const v = version.value
  if (!v) return
  form.title = v.title
  form.status = v.status
  form.notes = v.notes ?? ''
  form.lyrics = v.lyrics ?? ''
  form.bpm = v.bpm ?? ''          // NOUVEAU
  form.key = v.key ?? ''          // NOUVEAU
}
// Déclenché quand la version est REMPLACÉE, pas quand on ajoute un fichier dans version.files
watch(version, resetForm, { immediate: true })

const isDirty = computed(() => {
  const v = version.value
  if (!v) return false
  return form.title !== v.title
    || form.status !== v.status
    || form.notes !== (v.notes ?? '')
    || form.lyrics !== (v.lyrics ?? '')
    || form.bpm !== (v.bpm ?? '')         // NOUVEAU
    || form.key !== (v.key ?? '')         // NOUVEAU
})

// ---- Validation de confort (le serveur reste l'autorité) ----
const bpmError = computed(() => {
  if (form.bpm === '') return undefined
  return Number.isInteger(form.bpm) && form.bpm >= VERSION_BPM_MIN && form.bpm <= VERSION_BPM_MAX
    ? undefined
    : t('versions.detail.bpmRange', { versionBpmMin: VERSION_BPM_MIN, versionBpmMax: VERSION_BPM_MAX })
})

const keyError = computed(() => {
  const key = form.key.trim()
  return key === '' || MUSICAL_KEY_PATTERN.test(key) ? undefined : t('versions.detail.keyFormat')
})

const canSave = computed(() =>
  isDirty.value && !isSaving.value && form.title.trim() !== '' && !bpmError.value && !keyError.value)

// ---- Erreurs serveur ----
const fieldErrors = computed(() => mapProblemErrors(saveError.value))           // 400 : par champ
const globalSaveError = computed(() =>                                           // 422, 409, 500… : message global
  saveError.value && Object.keys(fieldErrors.value).length === 0 ? getErrorMessage(saveError.value) : null)

async function onSave() {
  if (!canSave.value) return
  await save({
    title: form.title.trim(),
    status: form.status,
    notes: form.notes.trim() || null,
    lyrics: form.lyrics.trim() ? form.lyrics : null,      // on garde les retours à la ligne internes
    bpm: form.bpm === '' ? null : form.bpm,               // NOUVEAU
    key: form.key.trim() || null,                         // NOUVEAU
  })
}

/** #25 : « Enregistrer dans cette tonalité » réécrit les accords (et la tonalité, si la version en a une). */
async function onSaveTransposed(lyrics: string, key: string | null) {
  form.lyrics = lyrics
  if (key) form.key = key
  await onSave()
}

// ================= Modifications non enregistrées =================
async function confirmLeave(): Promise<boolean> {
  if (!isDirty.value) return true
  return confirm({
    title: t('versions.detail.leaveTitle'),
    message: t('versions.detail.leaveMessage'),
    confirmLabel: t('versions.detail.leave'),
    danger: true,
  })
}
onBeforeRouteLeave(() => confirmLeave())
onBeforeRouteUpdate(() => confirmLeave())

function onBeforeUnload(event: BeforeUnloadEvent) {
  if (isDirty.value) event.preventDefault()
}
onMounted(() => window.addEventListener('beforeunload', onBeforeUnload))
onBeforeUnmount(() => window.removeEventListener('beforeunload', onBeforeUnload))

// ================= Fichiers (semaine 4) =================
const deletingId = ref<string | null>(null)
const fileError = ref<string | null>(null)

function onUploaded(file: SongFile) {
  version.value?.files.push(file)
}

// #15 : l'aperçu se prépare en arrière-plan ; on suit son état sans recharger le formulaire
usePreviewPolling(
  () => version.value?.files,
  async () => (await versionsApi.get(props.songId, props.versionId)).files,
)

async function onRetryPreview(file: SongFile) {
  deletingId.value = file.id
  fileError.value = null
  try {
    file.preview = (await filesApi.retryPreview(props.songId, props.versionId, file.id)).status
  } catch (e) {
    fileError.value = getErrorMessage(e)
  } finally {
    deletingId.value = null
  }
}

// #17 : analyse antivirus en échec, relancée à la demande
async function onRetryScan(file: SongFile) {
  deletingId.value = file.id
  fileError.value = null
  try {
    file.scan = (await filesApi.retryScan(props.songId, props.versionId, file.id)).status
  } catch (e) {
    fileError.value = getErrorMessage(e)
  } finally {
    deletingId.value = null
  }
}

async function onDeleteFile(file: SongFile) {
  if (!version.value) return
  const confirmed = await confirm({
    title: t('versions.detail.deleteFileTitle'),
    message: t('versions.detail.deleteFileMessage', { originalFileName: file.originalFileName }),
    confirmLabel: t('common.actions.delete'),
    danger: true,
  })
  if (!confirmed) return
  deletingId.value = file.id
  fileError.value = null
  try {
    await filesApi.remove(props.songId, version.value.id, file.id)
    version.value.files = version.value.files.filter((f) => f.id !== file.id)
    toast.success(t('versions.detail.fileTrashed'))
  } catch (e) {
    fileError.value = getErrorMessage(e)
  } finally {
    deletingId.value = null
  }
}

// ================= Suppression de la version (#20) =================
const isDeletingVersion = ref(false)
const versionError = ref<string | null>(null)

async function onDeleteVersion() {
  const v = version.value
  if (!v) return
  const confirmed = await confirm({
    title: t('versions.detail.deleteTitle'),
    message: t('versions.detail.deleteMessage', { number: v.number, title: v.title }),
    confirmLabel: t('common.actions.delete'),
    danger: true,
  })
  if (!confirmed) return
  isDeletingVersion.value = true
  versionError.value = null
  try {
    await versionsApi.remove(props.songId, v.id)
    resetForm()                     // plus rien à enregistrer : « Quitter sans enregistrer ? » ne se pose pas
    toast.success(t('versions.detail.deleted', { number: v.number }))
    await router.push({ name: 'song-detail', params: { songId: props.songId } })
  } catch (e) {
    versionError.value = getErrorMessage(e)     // 409 : une sortie l'utilise
  } finally {
    isDeletingVersion.value = false
  }
}

// ================= Lecture (semaine 4) =================
function trackOf(file: SongFile): Track {
  return toTrack({
    songId: props.songId, songTitle: song.value?.title ?? '',
    versionId: props.versionId, versionNumber: version.value?.number ?? 0, file,
  })
}

/** #15 : la forme d'onde du fichier en cours de lecture se colore au fil de l'écoute. */
const playing = computed(() => {
  const id = player.track?.id
  return id && player.duration > 0 ? { fileId: id, progress: player.currentTime / player.duration } : null
})

function onSeek(file: SongFile, seconds: number) {
  if (!version.value || !song.value) return
  commentedId.value = file.id
  if (player.track?.id === file.id) player.seek(seconds)
  else void player.playAt(trackOf(file), seconds)
}

function onPlay(file: SongFile) {
  if (!version.value || !song.value) return
  commentedId.value = file.id            // les commentaires suivent le fichier écouté
  void player.play(trackOf(file))
}

// ================= Commentaires (#8) =================
const audioFiles = computed(() => version.value?.files.filter((f) => f.fileType === 'Audio') ?? [])
const commentedId = ref<string | null>(null)
/** Fichier dont on affiche les commentaires : celui choisi, sinon le premier fichier audio. */
const commentedFile = computed(() =>
  audioFiles.value.find((f) => f.id === commentedId.value) ?? audioFiles.value[0] ?? null)
</script>

<template>
  <section>
    <RouterLink :to="{ name: 'song-detail', params: { songId } }">← {{ song?.title ?? t('versions.detail.backToSong') }}</RouterLink>

    <LoadingState v-if="isLoading" />
    <div v-else-if="isNotFound" class="card" role="alert"><p>{{ t('versions.detail.notFound') }}</p></div>
    <ErrorState v-else-if="error" :message="getErrorMessage(error)" @retry="reload()" />

    <template v-else-if="version">
      <header class="page-header">
        <h1>v{{ version.number }} — {{ version.title }}</h1>
        <StatusBadge :status="version.status" />
        <button v-if="canWrite" type="button" class="danger" :disabled="isDeletingVersion" @click="onDeleteVersion">
          {{ t('versions.detail.delete') }}
        </button>
      </header>
      <p v-if="versionError" class="field-error" role="alert">{{ versionError }}</p>
      <p class="muted">
        <small>
          {{ t('versions.detail.dates', { createdAt: formatDate(version.createdAt), updatedAt: formatDate(version.updatedAt) }) }}
          <template v-if="version.bpm || version.key">
            · {{ version.bpm ? `${version.bpm} BPM` : '' }} {{ version.key ?? '' }}
          </template>
        </small>
      </p>

      <nav class="version-nav" :aria-label="t('versions.detail.navLabel')">
        <RouterLink v-if="previous" :to="{ name: 'version-detail', params: { songId, versionId: previous.id } }">
          ← v{{ previous.number }}
        </RouterLink>
        <span v-else />
        <RouterLink v-if="next" :to="{ name: 'version-detail', params: { songId, versionId: next.id } }">
          v{{ next.number }} →
        </RouterLink>
      </nav>

        <form novalidate @submit.prevent="onSave">
        <!-- #2 : un fieldset désactivé désactive tous les champs qu'il contient -->
        <fieldset class="bare" :disabled="!canWrite">
        <SectionCard :title="t('versions.detail.info')">
            <div class="field">
              <label for="v-title">{{ t('versions.detail.titleLabel') }}</label>
              <input id="v-title" v-model="form.title" :maxlength="VERSION_TITLE_MAX" />
              <p v-if="fieldErrors.title" class="field-error">{{ fieldErrors.title }}</p>
            </div>

            <div class="field">
              <label for="v-status">{{ t('versions.detail.status') }}</label>
              <StatusSelect id="v-status" v-model="form.status" />
            </div>

            <!-- NOUVEAU : BPM et tonalité côte à côte -->
            <div class="row">
              <div class="field">
                <label for="v-bpm">BPM</label>
                <input id="v-bpm" v-model="form.bpm" type="number" inputmode="numeric" step="1"
                      :min="VERSION_BPM_MIN" :max="VERSION_BPM_MAX" :placeholder="t('versions.detail.bpmPlaceholder')"
                      :aria-invalid="!!(bpmError || fieldErrors.bpm)" aria-describedby="v-bpm-error" />
                <p v-if="bpmError || fieldErrors.bpm" id="v-bpm-error" class="field-error">
                  {{ bpmError ?? fieldErrors.bpm }}
                </p>
              </div>

              <div class="field">
                <label for="v-key">{{ t('versions.detail.key') }}</label>
                <input id="v-key" v-model="form.key" :maxlength="MUSICAL_KEY_MAX" :placeholder="t('versions.detail.keyPlaceholder')"
                      autocomplete="off" spellcheck="false"
                      :aria-invalid="!!(keyError || fieldErrors.key)" aria-describedby="v-key-error" />
                <p v-if="keyError || fieldErrors.key" id="v-key-error" class="field-error">
                  {{ keyError ?? fieldErrors.key }}
                </p>
              </div>
            </div>
          </SectionCard>

          <SectionCard :title="t('versions.detail.notes')">
            <textarea v-model="form.notes" rows="4" :maxlength="VERSION_NOTES_MAX" :aria-label="t('versions.detail.notes')" />
          </SectionCard>

    </fieldset>

        <!-- #25 : hors du fieldset, pour qu'un invité puisse transposer ; le panneau désactive lui-même le texte -->
        <SectionCard :title="t('versions.detail.lyrics')">
          <LyricsPanel :key="version.id" v-model="form.lyrics" :musical-key="form.key" :can-write="canWrite"
                       :dirty="isDirty" @save-transposed="onSaveTransposed" />
        </SectionCard>
        <div v-if="canWrite" class="save-bar">
          <span v-if="isDirty" class="muted" role="status">{{ t('versions.detail.unsaved') }}</span>
          <p v-if="globalSaveError" class="field-error" role="alert">{{ globalSaveError }}</p>
          <button type="button" class="secondary" :disabled="!isDirty || isSaving" @click="resetForm">{{ t('common.actions.cancel') }}</button>
          <button type="submit" :disabled="!canSave">{{ isSaving ? t('common.actions.saving') : t('common.actions.save') }}</button>
        </div>
      </form>

      <SectionCard :title="t('versions.detail.files')">
        <p v-if="fileError" class="field-error" role="alert">{{ fileError }}</p>
        <FileList :song-id="songId" :version-id="version.id" :files="version.files" :readonly="!canWrite"
                                    :busy-id="deletingId" :playing="playing"
                  @play="onPlay" @delete="onDeleteFile" @seek="onSeek" @retry="onRetryPreview"
                  @retry-scan="onRetryScan" />
        <FileUploader v-if="canWrite" :song-id="songId" :version-id="version.id" @uploaded="onUploaded" />
      </SectionCard>
      <StemsSection v-if="audioFiles.length > 0" :song-id="songId" :version-id="version.id" :files="version.files"
                    :readonly="!canWrite" />
      <ShareLinksPanel v-if="canWrite" :target="{ type: 'Version', id: version.id }" />
      <SectionCard v-if="commentedFile" :title="t('versions.detail.comments')">
        <template v-if="audioFiles.length > 1" #actions>
          <select :value="commentedFile.id" :aria-label="t('versions.detail.commentedFile')" class="commented-file"
                  @change="commentedId = ($event.target as HTMLSelectElement).value">
            <option v-for="file in audioFiles" :key="file.id" :value="file.id">{{ file.originalFileName }}</option>
          </select>
        </template>
        <CommentsPanel :target="{ songId, versionId: version.id, fileId: commentedFile.id }"
                       :track="trackOf(commentedFile)" />
      </SectionCard>
    </template>
  </section>
</template>

<style scoped>
.version-nav { display: flex; justify-content: space-between; margin: 1rem 0; }
.row { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
.lyrics { font-family: inherit; line-height: 1.5; }
.save-bar { display: flex; gap: .75rem; align-items: center; justify-content: flex-end; margin-bottom: 1rem; flex-wrap: wrap; }
@media (max-width: 600px) { .row { grid-template-columns: 1fr; } }
.bare { border: 0; padding: 0; margin: 0; min-width: 0; }
.commented-file { width: auto; max-width: 16rem; }
</style>