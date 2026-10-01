<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import SongForm from '../components/SongForm.vue'
import { songsApi } from '../api/songsApi'
import type { SongInput } from '../types'
import { useAsyncAction } from '@/shared/composables/useAsyncAction'
import { mapProblemErrors } from '@/shared/api/mapProblemErrors'
import { getErrorMessage } from '@/shared/api/ApiError'
import { useSongsStore } from '../stores/songsStore'

const router = useRouter()
const store = useSongsStore()
const { isLoading, error, run } = useAsyncAction(songsApi.create)

const fieldErrors = computed(() => mapProblemErrors(error.value))
// Message global uniquement si l'erreur ne concerne pas un champ précis
const globalError = computed(() =>
  error.value && Object.keys(fieldErrors.value).length === 0 ? getErrorMessage(error.value) : null,
)

async function onSubmit(input: SongInput) {
  const song = await run(input)
  if (song) {
    store.upsert(song)
    await router.push({ name: 'song-detail', params: { songId: song.id } })
  }
}
</script>

<template>
  <section>
    <h1>Nouveau morceau</h1>
    <p v-if="globalError" class="field-error" role="alert">{{ globalError }}</p>
    <SongForm
      submit-label="Créer"
      :submitting="isLoading"
      :server-errors="fieldErrors"
      @submit="onSubmit"
      @cancel="router.push({ name: 'songs' })"
    />
  </section>
</template>
