<script setup lang="ts">
import { onMounted } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'
import { useSongsStore } from '../stores/songsStore'
import SongCard from '../components/SongCard.vue'
import LoadingState from '@/shared/components/LoadingState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import { getErrorMessage } from '@/shared/api/ApiError'

const store = useSongsStore()
const { items, isLoading, isLoaded, error } = storeToRefs(store)   // réactif
const router = useRouter()

function onSelect(id: string) {
  router.push({ name: 'song-detail', params: { songId: id } })
}

onMounted(() => store.fetchList())
</script>

<template>
  <section>
    <header class="page-header">
      <h1>Morceaux</h1>
      <RouterLink :to="{ name: 'song-create' }">+ Nouveau morceau</RouterLink>
    </header>

    <LoadingState v-if="isLoading || (!isLoaded && !error)" />
    <ErrorState v-else-if="error" :message="getErrorMessage(error)" @retry="store.fetchList(true)" />
    <EmptyState v-else-if="items.length === 0" message="Aucun morceau pour l'instant.">
      <RouterLink :to="{ name: 'song-create' }">Créer le premier</RouterLink>
    </EmptyState>
    <ul v-else class="song-list">
      <li v-for="song in items" :key="song.id">
        <SongCard :song="song" @select="onSelect" />
      </li>
    </ul>
  </section>
</template>

<style scoped>
.song-list { list-style: none; padding: 0; display: grid; gap: .75rem; }
</style>
