<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { songsApi } from '../api/songsApi'
import type { Song } from '../types'
import SongCard from '../components/SongCard.vue'
import LoadingState from '@/shared/components/LoadingState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import { getErrorMessage } from '@/shared/api/ApiError'
import { useRouter } from 'vue-router'

const songs = ref<Song[]>([])
const isLoading = ref(true)
const error = ref<unknown>(null)
const router = useRouter()

async function load() {
  isLoading.value = true
  error.value = null
  try {
    songs.value = (await songsApi.list()).items
  } catch (e) {
    error.value = e
  } finally {
    isLoading.value = false
  }
}

function onSelect(id: string) {
  router.push({ name: 'song-detail', params: { songId: id } })
}

onMounted(load)
</script>

<template>
  <section>
  <header class="page-header">
    <h1>Morceaux</h1>
    <RouterLink :to="{ name: 'song-create' }">+ Nouveau morceau</RouterLink>
  </header>

    <LoadingState v-if="isLoading" />
    <ErrorState v-else-if="error" :message="getErrorMessage(error)" @retry="load" />
    <EmptyState v-else-if="songs.length === 0" message="Aucun morceau pour l'instant." />
    <ul v-else class="song-list">
      <li v-for="song in songs" :key="song.id">
        <SongCard :song="song" @select="onSelect" />
      </li>
    </ul>
  </section>
</template>

<style scoped>
.song-list { list-style: none; padding: 0; display: grid; gap: .75rem; }
</style>
