<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { onBeforeRouteLeave, onBeforeRouteUpdate, RouterLink } from 'vue-router'
import { useVersion } from '../composables/useVersion'
import StatusBadge from '../components/StatusBadge.vue'
import StatusSelect from '../components/StatusSelect.vue'
import {
  MUSICAL_KEY_MAX, MUSICAL_KEY_PATTERN,
  VERSION_BPM_MAX, VERSION_BPM_MIN,
  VERSION_LYRICS_MAX, VERSION_NOTES_MAX, VERSION_TITLE_MAX,
  type SongVersionStatus,
} from '../types'
import FileList from '@/features/files/components/FileList.vue'
import FileUploader from '@/features/files/components/FileUploader.vue'
import type { SongFile } from '@/features/files/types'
import { usePlayerStore } from '@/features/player/stores/playerStore'
import { toTrack } from '@/features/player/track'
import SectionCard from '@/shared/components/SectionCard.vue'
import LoadingState from '@/shared/components/LoadingState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import { ApiError, getErrorMessage } from '@/shared/api/ApiError'
import { mapProblemErrors } from '@/shared/api/mapProblemErrors'
import { formatDate } from '@/shared/utils/format'
import { useConfirm } from '@/shared/composables/useConfirm'
import { useToast } from '@/shared/composables/useToast'

const props = defineProps<{ songId: string; versionId: string }>()

const { song, version, previous, next, isLoading, error, isSaving, saveError, save, reload } =
  useVersion(() => props.songId, () => props.versionId)
const player = usePlayerStore()

const isNotFound = computed(() => error.value instanceof ApiError && error.value.status === 404)
const { confirm } = useConfirm()
const toast = useToast()
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
    : `Nombre entier entre ${VERSION_BPM_MIN} et ${VERSION_BPM_MAX}.`
})

const keyError = computed(() => {
  const key = form.key.trim()
  return key === '' || MUSICAL_KEY_PATTERN.test(key) ? undefined : 'Format attendu : C, F#m, Bb…'
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

// ================= Modifications non enregistrées =================
async function confirmLeave(): Promise<boolean> {
  if (!isDirty.value) return true
  return confirm({
    title: 'Quitter sans enregistrer ?',
    message: 'Vos modifications de cette version seront perdues.',
    confirmLabel: 'Quitter',
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

async function onDeleteFile(file: SongFile) {
  if (!version.value) return
  const confirmed = await confirm({
    title: 'Supprimer ce fichier ?',
    message: `« ${file.originalFileName} » sera supprimé définitivement.`,
    confirmLabel: 'Supprimer',
    danger: true,
  })
  if (!confirmed) return
  // … suite inchangée, puis en cas de succès :
  toast.success('Fichier supprimé.')
}

// ================= Lecture (semaine 4) =================
function onPlay(file: SongFile) {
  if (!version.value || !song.value) return
  void player.play(toTrack({
    songId: props.songId, songTitle: song.value.title,
    versionId: version.value.id, versionNumber: version.value.number, file,
  }))
}
</script>

<template>
  <section>
    <RouterLink :to="{ name: 'song-detail', params: { songId } }">← {{ song?.title ?? 'Retour au morceau' }}</RouterLink>

    <LoadingState v-if="isLoading" />
    <div v-else-if="isNotFound" class="card" role="alert"><p>Cette version n'existe pas ou a été supprimée.</p></div>
    <ErrorState v-else-if="error" :message="getErrorMessage(error)" @retry="reload()" />

    <template v-else-if="version">
      <header class="page-header">
        <h1>v{{ version.number }} — {{ version.title }}</h1>
        <StatusBadge :status="version.status" />
      </header>
      <p class="muted">
        <small>
          Créée le {{ formatDate(version.createdAt) }} · modifiée le {{ formatDate(version.updatedAt) }}
          <template v-if="version.bpm || version.key">
            · {{ version.bpm ? `${version.bpm} BPM` : '' }} {{ version.key ?? '' }}
          </template>
        </small>
      </p>

      <nav class="version-nav" aria-label="Navigation entre versions">
        <RouterLink v-if="previous" :to="{ name: 'version-detail', params: { songId, versionId: previous.id } }">
          ← v{{ previous.number }}
        </RouterLink>
        <span v-else />
        <RouterLink v-if="next" :to="{ name: 'version-detail', params: { songId, versionId: next.id } }">
          v{{ next.number }} →
        </RouterLink>
      </nav>

      <form novalidate @submit.prevent="onSave">
        <SectionCard title="Informations">
          <div class="field">
            <label for="v-title">Titre *</label>
            <input id="v-title" v-model="form.title" :maxlength="VERSION_TITLE_MAX" />
            <p v-if="fieldErrors.title" class="field-error">{{ fieldErrors.title }}</p>
          </div>

          <div class="field">
            <label for="v-status">Statut</label>
            <StatusSelect id="v-status" v-model="form.status" />
          </div>

          <!-- NOUVEAU : BPM et tonalité côte à côte -->
          <div class="row">
            <div class="field">
              <label for="v-bpm">BPM</label>
              <input id="v-bpm" v-model="form.bpm" type="number" inputmode="numeric" step="1"
                     :min="VERSION_BPM_MIN" :max="VERSION_BPM_MAX" placeholder="ex. 92"
                     :aria-invalid="!!(bpmError || fieldErrors.bpm)" aria-describedby="v-bpm-error" />
              <p v-if="bpmError || fieldErrors.bpm" id="v-bpm-error" class="field-error">
                {{ bpmError ?? fieldErrors.bpm }}
              </p>
            </div>

            <div class="field">
              <label for="v-key">Tonalité</label>
              <input id="v-key" v-model="form.key" :maxlength="MUSICAL_KEY_MAX" placeholder="ex. F#m"
                     autocomplete="off" spellcheck="false"
                     :aria-invalid="!!(keyError || fieldErrors.key)" aria-describedby="v-key-error" />
              <p v-if="keyError || fieldErrors.key" id="v-key-error" class="field-error">
                {{ keyError ?? fieldErrors.key }}
              </p>
            </div>
          </div>
        </SectionCard>

        <SectionCard title="Notes">
          <textarea v-model="form.notes" rows="4" :maxlength="VERSION_NOTES_MAX" aria-label="Notes" />
        </SectionCard>

        <SectionCard title="Paroles">
          <textarea v-model="form.lyrics" class="lyrics" rows="14" :maxlength="VERSION_LYRICS_MAX" aria-label="Paroles" />
        </SectionCard>

        <div class="save-bar">
          <span v-if="isDirty" class="muted" role="status">Modifications non enregistrées</span>
          <p v-if="globalSaveError" class="field-error" role="alert">{{ globalSaveError }}</p>
          <button type="button" class="secondary" :disabled="!isDirty || isSaving" @click="resetForm">Annuler</button>
          <button type="submit" :disabled="!canSave">{{ isSaving ? 'Enregistrement…' : 'Enregistrer' }}</button>
        </div>
      </form>

      <SectionCard title="Fichiers">
        <p v-if="fileError" class="field-error" role="alert">{{ fileError }}</p>
        <FileList :song-id="songId" :version-id="version.id" :files="version.files"
                  :busy-id="deletingId" @play="onPlay" @delete="onDeleteFile" />
        <FileUploader :song-id="songId" :version-id="version.id" @uploaded="onUploaded" />
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
</style>