<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { RouterLink } from 'vue-router'
import { songsApi } from '../api/songsApi'
import type { Song } from '../types'
import { versionsApi } from '@/features/versions/api/versionsApi'
import type { SongVersionSummary } from '@/features/versions/types'
import VersionList from '@/features/versions/components/VersionList.vue'
import LoadingState from '@/shared/components/LoadingState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import { ApiError, getErrorMessage } from '@/shared/api/ApiError'
import { formatDate } from '@/shared/utils/format'

const props = defineProps<{ songId: string }>()

const song = ref<Song | null>(null)
const versions = ref<SongVersionSummary[]>([])
const isLoading = ref(true)
const error = ref<unknown>(null)

const isNotFound = computed(() => error.value instanceof ApiError && error.value.status === 404)

async function load(id: string) {
  isLoading.value = true
  error.value = null
  try {
    // Les deux appels partent en parallèle (≈ Task.WhenAll)
    ;[song.value, versions.value] = await Promise.all([songsApi.get(id), versionsApi.list(id)])
  } catch (e) {
    error.value = e
  } finally {
    isLoading.value = false
  }
}

// immediate : charge au premier affichage ET à chaque changement de paramètre
watch(() => props.songId, load, { immediate: true })
</script>

<template>
  <section>
    <RouterLink to="/songs">← Tous les morceaux</RouterLink>

    <LoadingState v-if="isLoading" />
    <div v-else-if="isNotFound" class="card" role="alert">
      <p>Ce morceau n'existe pas ou a été supprimé.</p>
    </div>
    <ErrorState v-else-if="error" :message="getErrorMessage(error)" @retry="load(songId)" />

    <template v-else-if="song">
      <header class="page-header">
        <div>
          <h1>{{ song.title }}</h1>
          <p v-if="song.artist" class="muted">{{ song.artist }}</p>
        </div>
        <!-- boutons Modifier / Supprimer ajoutés demain -->
      </header>
      <p v-if="song.description">{{ song.description }}</p>
      <p class="muted"><small>Créé le {{ formatDate(song.createdAt) }} · modifié le {{ formatDate(song.updatedAt) }}</small></p>

      <h2>Versions</h2>
      <VersionList :versions="versions" />
    </template>
  </section>
</template>
