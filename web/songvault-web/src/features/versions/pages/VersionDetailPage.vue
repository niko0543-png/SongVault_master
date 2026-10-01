<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, watch } from 'vue'
import { onBeforeRouteLeave, onBeforeRouteUpdate, RouterLink } from 'vue-router'
import { useVersion } from '../composables/useVersion'
import StatusBadge from '../components/StatusBadge.vue'
import StatusSelect from '../components/StatusSelect.vue'
import { VERSION_LYRICS_MAX, VERSION_NOTES_MAX, VERSION_TITLE_MAX, type SongVersionStatus } from '../types'
import FileList from '@/features/files/components/FileList.vue'
import SectionCard from '@/shared/components/SectionCard.vue'
import LoadingState from '@/shared/components/LoadingState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import { ApiError, getErrorMessage } from '@/shared/api/ApiError'
import { mapProblemErrors } from '@/shared/api/mapProblemErrors'
import { formatDate } from '@/shared/utils/format'
import { ref } from 'vue'   // à ajouter à l'import existant
import FileUploader from '@/features/files/components/FileUploader.vue'
import { filesApi } from '@/features/files/api/filesApi'
import type { SongFile } from '@/features/files/types'
import { usePlayerStore } from '@/features/player/stores/playerStore'
import { toTrack } from '@/features/player/track'

const props = defineProps<{ songId: string; versionId: string }>()
const { song, version, previous, next, isLoading, error, isSaving, saveError, save, reload } =
  useVersion(() => props.songId, () => props.versionId)
const isNotFound = computed(() => error.value instanceof ApiError && error.value.status === 404)
const fieldErrors = computed(() => mapProblemErrors(saveError.value))
const deletingId = ref<string | null>(null)
const fileError = ref<string | null>(null)
const player = usePlayerStore()

// ---- Formulaire : copie locale de la version chargée ----
const form = reactive({ title: '', status: 'Idea' as SongVersionStatus, notes: '', lyrics: '' })

function resetForm() {
  if (!version.value) return
  form.title = version.value.title
  form.status = version.value.status
  form.notes = version.value.notes ?? ''
  form.lyrics = version.value.lyrics ?? ''
}
// Déclenché quand la version est REMPLACÉE (chargement, enregistrement),
// pas quand on ajoute un fichier dans version.files (watch non profond)
watch(version, resetForm, { immediate: true })

const isDirty = computed(() =>
  !!version.value && (
    form.title !== version.value.title ||
    form.status !== version.value.status ||
    form.notes !== (version.value.notes ?? '') ||
    form.lyrics !== (version.value.lyrics ?? '')))

async function onSave() {
  await save({
    title: form.title.trim(),
    status: form.status,
    notes: form.notes.trim() || null,
    lyrics: form.lyrics.trim() ? form.lyrics : null,   // on garde les retours à la ligne internes
  })
}

// ---- Protection des modifications non enregistrées ----
function confirmLeave(): boolean {
  return !isDirty.value || window.confirm('Des modifications ne sont pas enregistrées. Quitter quand même ?')
}
onBeforeRouteLeave(() => confirmLeave())      // vers une autre page
onBeforeRouteUpdate(() => confirmLeave())     // vers une autre version (même composant)

function onBeforeUnload(event: BeforeUnloadEvent) {
  if (isDirty.value) event.preventDefault()   // fermeture d'onglet, F5
}

function onUploaded(file: SongFile) {
  version.value?.files.push(file)       // ajout en place : le formulaire n'est pas réinitialisé
}

async function onDeleteFile(file: SongFile) {
  if (!version.value || !window.confirm(`Supprimer « ${file.originalFileName} » ?`)) return
  deletingId.value = file.id
  fileError.value = null
  try {
    await filesApi.remove(props.songId, version.value.id, file.id)
    version.value.files = version.value.files.filter((f) => f.id !== file.id)   // après la réponse
  } catch (e) {
    fileError.value = getErrorMessage(e)
  } finally {
    deletingId.value = null
  }
}

function onPlay(file: SongFile) {
  if (!version.value || !song.value) return
  void player.play(toTrack({
    songId: props.songId, songTitle: song.value.title,
    versionId: version.value.id, versionNumber: version.value.number, file,
  }))
}

onMounted(() => window.addEventListener('beforeunload', onBeforeUnload))
onBeforeUnmount(() => window.removeEventListener('beforeunload', onBeforeUnload))
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
      <p class="muted"><small>Créée le {{ formatDate(version.createdAt) }} · modifiée le {{ formatDate(version.updatedAt) }}</small></p>

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
        </SectionCard>

        <SectionCard title="Notes">
          <textarea v-model="form.notes" rows="4" :maxlength="VERSION_NOTES_MAX" aria-label="Notes" />
        </SectionCard>

        <SectionCard title="Paroles">
          <textarea v-model="form.lyrics" class="lyrics" rows="14" :maxlength="VERSION_LYRICS_MAX" aria-label="Paroles" />
        </SectionCard>

        <div class="save-bar">
          <span v-if="isDirty" class="muted" role="status">Modifications non enregistrées</span>
          <p v-if="saveError && !fieldErrors.title" class="field-error" role="alert">{{ getErrorMessage(saveError) }}</p>
          <button type="button" class="secondary" :disabled="!isDirty || isSaving" @click="resetForm">Annuler</button>
          <button type="submit" :disabled="!isDirty || isSaving || !form.title.trim()">
            {{ isSaving ? 'Enregistrement…' : 'Enregistrer' }}
          </button>
        </div>
      </form>

<SectionCard title="Fichiers">
  <p v-if="fileError" class="field-error" role="alert">{{ fileError }}</p>
  <FileList :song-id="songId" :version-id="version.id" :files="version.files"
            :busy-id="deletingId" @delete="onDeleteFile" @play="onPlay"/>
  <FileUploader :song-id="songId" :version-id="version.id" @uploaded="onUploaded" />
</SectionCard>
    </template>
  </section>
</template>

<style scoped>
.version-nav { display: flex; justify-content: space-between; margin: 1rem 0; }
.lyrics { font-family: inherit; line-height: 1.5; }
.save-bar { display: flex; gap: .75rem; align-items: center; justify-content: flex-end; margin-bottom: 1rem; }
</style>
