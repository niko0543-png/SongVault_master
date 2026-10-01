<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import SongForm from '../components/SongForm.vue'
import { songsApi } from '../api/songsApi'
import type { SongInput } from '../types'
import { useAsyncAction } from '@/shared/composables/useAsyncAction'
import { mapProblemErrors } from '@/shared/api/mapProblemErrors'
import { getErrorMessage } from '@/shared/api/ApiError'
import LoadingState from '@/shared/components/LoadingState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'

const props = defineProps<{ songId: string }>()
const router = useRouter()

const loader = useAsyncAction(songsApi.get)
const saver = useAsyncAction(async (input: SongInput) => {
  await songsApi.update(props.songId, input)
  return songsApi.get(props.songId)          // PUT renvoie 204 : on relit la version à jour
})

const song = computed(() => loader.lastResult.value)
const fieldErrors = computed(() => mapProblemErrors(saver.error.value))
const globalError = computed(() =>
  saver.error.value && Object.keys(fieldErrors.value).length === 0 ? getErrorMessage(saver.error.value) : null,
)

async function onSubmit(input: SongInput) {
  const updated = await saver.run(input)
  if (updated) await router.push({ name: 'song-detail', params: { songId: updated.id } })
}

onMounted(() => loader.run(props.songId))
</script>

<template>
  <section>
    <h1>Modifier le morceau</h1>
    <LoadingState v-if="loader.isLoading.value" />
    <ErrorState v-else-if="loader.error.value" :message="getErrorMessage(loader.error.value)" @retry="loader.run(songId)" />
    <template v-else-if="song">
      <p v-if="globalError" class="field-error" role="alert">{{ globalError }}</p>
      <SongForm
        :initial="{ title: song.title, artist: song.artist, description: song.description }"
        :submitting="saver.isLoading.value"
        :server-errors="fieldErrors"
        @submit="onSubmit"
        @cancel="router.push({ name: 'song-detail', params: { songId } })"
      />
    </template>
  </section>
</template>
