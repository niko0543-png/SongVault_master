<script setup lang="ts">
import type { Song } from '../types'
import { formatDate } from '@/shared/utils/format'

defineProps<{ song: Song }>()
defineEmits<{ select: [id: string] }>()
</script>

<template>
  <article class="card song-card" data-testid="song-card">
    <h2>
      <button type="button" class="link" @click="$emit('select', song.id)">{{ song.title }}</button>
    </h2>
    <p v-if="song.artist" class="muted">{{ song.artist }}</p>
    <p class="muted"><small>Modifié le {{ formatDate(song.updatedAt) }}</small></p>
  </article>
</template>

<style scoped>
.song-card h2 { margin: 0 0 .25rem; font-size: 1.1rem; }
.link { background: none; border: none; color: var(--primary); padding: 0; font-size: inherit; text-align: left; }
</style>
