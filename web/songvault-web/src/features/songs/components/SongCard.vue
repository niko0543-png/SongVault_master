<script setup lang="ts">
import type { Song } from '../types'
import SongBadgeTag from './SongBadgeTag.vue'
import { formatDate } from '@/shared/utils/format'
import { t } from '@/shared/i18n'

defineProps<{ song: Song }>()
defineEmits<{ select: [id: string] }>()
</script>

<template>
  <article class="card song-card" data-testid="song-card">
    <h2>
      <button type="button" class="link" @click="$emit('select', song.id)">{{ song.title }}</button>
        <SongBadgeTag :badge="song.badge" />
    </h2>
    <p v-if="song.artist" class="muted">{{ song.artist }}</p>
    <p class="muted"><small>{{ t('songs.card.updated', { updatedAt: formatDate(song.updatedAt) }) }}</small></p>
  </article>
</template>

<style scoped>
.song-card h2 { margin: 0 0 .25rem; font-size: 1.1rem; display: flex; align-items: center; gap: .5rem; flex-wrap: wrap; }
.link { background: none; border: none; color: var(--primary); padding: 0; font-size: inherit; text-align: left; }
</style>
