<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { songsApi } from '../api/songsApi'
import type { Song } from '../types'
import { useSongsStore } from '../stores/songsStore'
import { versionsApi } from '@/features/versions/api/versionsApi'
import type { SongVersionInput, SongVersionStatus, SongVersionSummary } from '@/features/versions/types'
import VersionTimeline from '@/features/versions/components/VersionTimeline.vue'
import CreateVersionForm from '@/features/versions/components/CreateVersionForm.vue'
import LoadingState from '@/shared/components/LoadingState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import { ApiError, getErrorMessage } from '@/shared/api/ApiError'
import { mapProblemErrors } from '@/shared/api/mapProblemErrors'
import { useAsyncAction } from '@/shared/composables/useAsyncAction'
import { formatDate } from '@/shared/utils/format'

const props = defineProps<{ songId: string }>()
const router = useRouter()
const store = useSongsStore()

// ---- Chargement ----
const song = ref<Song | null>(null)
const versions = ref<SongVersionSummary[]>([])
const isLoading = ref(true)
const error = ref<unknown>(null)
const isNotFound = computed(() => error.value instanceof ApiError && error.value.status === 404)

async function load(id: string) {
  isLoading.value = true
  error.value = null
  try {
    ;[song.value, versions.value] = await Promise.all([songsApi.get(id), versionsApi.list(id)])
  } catch (e) {
    error.value = e
  } finally {
    isLoading.value = false
  }
}
watch(() => props.songId, load, { immediate: true })

// ---- Création d'une version ----
const creation = useAsyncAction((input: SongVersionInput) => versionsApi.create(props.songId, input))
const creationErrors = computed(() => mapProblemErrors(creation.error.value))
const formKey = ref(0)
const lastCreated = ref<number | null>(null)

async function onCreateVersion(input: SongVersionInput) {
  const created = await creation.run(input)
  if (!created) return
  // Pessimiste : on ajoute ce que le SERVEUR a renvoyé, avec SON numéro
  versions.value = [...versions.value, {
    id: created.id, number: created.number, title: created.title, status: created.status, createdAt: created.createdAt,
  }]
  lastCreated.value = created.number
  formKey.value++          // change la clé : Vue recrée le formulaire, vide
  store.invalidate()       // la date de modification du morceau a changé
}

// ---- Changement de statut ----
const savingId = ref<string | null>(null)
const statusError = ref<string | null>(null)

async function onChangeStatus(versionId: string, status: SongVersionStatus) {
  savingId.value = versionId
  statusError.value = null
  try {
    const updated = await versionsApi.changeStatus(props.songId, versionId, status)
    versions.value = versions.value.map((v) => (v.id === versionId ? { ...v, status: updated.status } : v))
  } catch (e) {
    statusError.value = getErrorMessage(e)
  } finally {
    savingId.value = null
  }
}

// ---- Suppression du morceau (semaine 3) ----
const deletion = useAsyncAction(songsApi.remove)

async function onDelete() {
  if (!song.value) return
  const id = song.value.id
  if (!window.confirm(`Supprimer « ${song.value.title} », ses versions et ses fichiers ? Cette action est définitive.`)) return
  await deletion.run(id)
  if (!deletion.error.value) {
    store.remove(id)
    await router.push({ name: 'songs' })
  }
}
</script>

<template>
  <section>
    <RouterLink to="/songs">← Tous les morceaux</RouterLink>

    <LoadingState v-if="isLoading" />
    <div v-else-if="isNotFound" class="card" role="alert"><p>Ce morceau n'existe pas ou a été supprimé.</p></div>
    <ErrorState v-else-if="error" :message="getErrorMessage(error)" @retry="load(songId)" />

    <template v-else-if="song">
      <header class="page-header">
        <div>
          <h1>{{ song.title }}</h1>
          <p v-if="song.artist" class="muted">{{ song.artist }}</p>
        </div>
        <div class="actions">
          <RouterLink :to="{ name: 'song-edit', params: { songId: song.id } }">Modifier</RouterLink>
          <button type="button" class="danger" :disabled="deletion.isLoading.value" @click="onDelete">Supprimer</button>
        </div>
      </header>
      <p v-if="deletion.error.value" class="field-error" role="alert">{{ getErrorMessage(deletion.error.value) }}</p>
      <p v-if="song.description">{{ song.description }}</p>
      <p class="muted"><small>Créé le {{ formatDate(song.createdAt) }} · modifié le {{ formatDate(song.updatedAt) }}</small></p>

      <h2>Versions</h2>
      <RouterLink v-if="versions.length >= 2" :to="{ name: 'version-compare', params: { songId: song.id } }">
        Comparer des versions
      </RouterLink>
      <p v-if="lastCreated" class="muted" role="status">Version v{{ lastCreated }} créée.</p>
      <p v-if="statusError" class="field-error" role="alert">{{ statusError }}</p>
      <VersionTimeline :song-id="song.id" :versions="versions" :saving-id="savingId" @change-status="onChangeStatus" />

      <CreateVersionForm
        :key="formKey"
        :submitting="creation.isLoading.value"
        :server-errors="creationErrors"
        @submit="onCreateVersion"
      />
    </template>
  </section>
</template>

<style scoped>
.actions { display: flex; gap: .75rem; align-items: center; }
</style>
