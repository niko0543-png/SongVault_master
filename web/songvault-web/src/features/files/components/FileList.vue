<script setup lang="ts">
import type { SongFile, SongFileType } from '../types'
import { filesApi } from '../api/filesApi'
import { formatDate, formatFileSize } from '@/shared/utils/format'

defineProps<{ songId: string; versionId: string; files: SongFile[]; busyId?: string | null }>()
defineEmits<{ delete: [file: SongFile] }>()

const icons: Record<SongFileType, string> = { Audio: '🎵', Tablature: '🎸', Document: '📄' }
</script>

<template>
  <p v-if="files.length === 0" class="muted">Aucun fichier pour cette version.</p>
  <ul v-else class="file-list">
    <li v-for="file in files" :key="file.id" class="file-item" data-testid="file-item">
      <span aria-hidden="true">{{ icons[file.fileType] }}</span>
      <div class="file-info">
        <span>{{ file.originalFileName }}</span>
        <small class="muted">{{ formatFileSize(file.sizeBytes) }} · {{ formatDate(file.uploadedAt) }}</small>
      </div>
      <div class="file-actions">
        <a :href="filesApi.contentUrl(songId, versionId, file.id, true)" download>Télécharger</a>
        <button type="button" class="secondary" :disabled="busyId === file.id" @click="$emit('delete', file)">Supprimer</button>
      </div>
    </li>
  </ul>
</template>

<style scoped>
.file-list { list-style: none; padding: 0; display: grid; gap: .5rem; }
.file-item { display: grid; grid-template-columns: auto 1fr auto; gap: .75rem; align-items: center; }
.file-info { display: grid; overflow-wrap: anywhere; }
.file-actions { display: flex; gap: .5rem; align-items: center; }
</style>
