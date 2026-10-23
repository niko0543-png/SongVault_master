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
        <!-- #34 : la tablature s'ouvre d'ici, sans passer par le morceau et la version -->
        <RouterLink v-if="song.tablatureVersionId && song.tablatureFileId" class="tab-icon" data-testid="song-tablature"
                    :to="{ name: 'tablature', params: { songId: song.id, versionId: song.tablatureVersionId, fileId: song.tablatureFileId } }"
                    :aria-label="t('songs.card.openTablature', { title: song.title })" :title="t('songs.card.openTablature', { title: song.title })">
          🎸
        </RouterLink>
    </h2>
    <p v-if="song.artist" class="muted">{{ song.artist }}</p>
    <p class="muted"><small>{{ t('songs.card.updated', { updatedAt: formatDate(song.updatedAt) }) }}</small></p>
  </article>
</template>

<style scoped>
.song-card h2 { margin: 0 0 .25rem; font-size: 1.1rem; display: flex; align-items: center; gap: .5rem; flex-wrap: wrap; }
.tab-icon { text-decoration: none; font-size: 1rem; }
.link { background: none; border: none; color: var(--primary); padding: 0; font-size: inherit; text-align: left; }
</style>
