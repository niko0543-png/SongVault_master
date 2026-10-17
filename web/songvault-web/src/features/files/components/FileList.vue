<script setup lang="ts">
import { isReadable, type SongFile, type SongFileType } from '../types'
import { filesApi } from '../api/filesApi'
import FileWaveform from './FileWaveform.vue'
import { formatDate, formatFileSize } from '@/shared/utils/format'

defineProps<{
  songId: string
  versionId: string
  files: SongFile[]
  busyId?: string | null
  readonly?: boolean
  /** #15 : fichier en cours de lecture et avancement (0 à 1), pour colorer sa forme d'onde. */
  playing?: { fileId: string; progress: number } | null
}>()
defineEmits<{
  play: [file: SongFile]
  delete: [file: SongFile]
  seek: [file: SongFile, seconds: number]
  retry: [file: SongFile]
  retryScan: [file: SongFile]
}>()

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
        <small v-if="file.scan === 'Pending'" class="muted" role="status">Analyse antivirus en cours…</small>
        <small v-else-if="file.scan === 'Infected'" class="preview-failed" data-testid="scan-infected">
          Bloqué : l'antivirus a détecté une menace ({{ file.scanThreat }}). Ce fichier ne peut être ni écouté ni téléchargé.
        </small>
        <small v-else-if="file.scan === 'Failed'" class="preview-failed">
          L'analyse antivirus a échoué.
          <button v-if="!readonly" type="button" class="link" :disabled="busyId === file.id"
                  @click="$emit('retryScan', file)">Relancer l'analyse</button>
        </small>
        <small v-else-if="file.preview === 'Pending'" class="muted" role="status">Préparation de l'aperçu…</small>
        <small v-else-if="file.preview === 'Failed'" class="preview-failed">
          Aperçu indisponible : l'écoute utilise le fichier original.
          <button v-if="!readonly" type="button" class="link" :disabled="busyId === file.id"
                  @click="$emit('retry', file)">Réessayer</button>
        </small>
      </div>
      <div class="file-actions">
        <template v-if="isReadable(file)">
          <button v-if="file.fileType === 'Audio'" type="button" class="secondary" @click="$emit('play', file)">▶ Écouter</button>
          <a :href="filesApi.contentUrl(songId, versionId, file.id, true)" download>Télécharger</a>
        </template>
        <button v-if="!readonly" type="button" class="secondary" :disabled="busyId === file.id" @click="$emit('delete', file)">Supprimer</button>
      </div>
      <FileWaveform v-if="file.preview === 'Ready' && isReadable(file)" class="file-waveform"
                    :song-id="songId" :version-id="versionId" :file-id="file.id" :label="file.originalFileName"
                    :progress="playing?.fileId === file.id ? playing.progress : 0"
                    @seek="(seconds) => $emit('seek', file, seconds)" />
    </li>
  </ul>
</template>

<style scoped>
.file-list { list-style: none; padding: 0; display: grid; gap: .5rem; }
.file-item { display: grid; grid-template-columns: auto 1fr auto; gap: .75rem; align-items: center; }
.file-info { display: grid; overflow-wrap: anywhere; min-width: 0; }
.file-actions { display: flex; gap: .5rem; align-items: center; flex-wrap: wrap; }
/* #15 : la forme d'onde occupe toute la largeur, sous le nom et les boutons */
.file-waveform { grid-column: 1 / -1; }
.preview-failed { color: var(--danger); }
.link { background: none; border: 0; padding: 0; color: var(--primary); text-decoration: underline; cursor: pointer; font: inherit; }
/* #14 : téléphone. Les boutons passent sous le nom du fichier, qui garde toute la largeur */
@media (max-width: 600px) {
  .file-item { grid-template-columns: auto minmax(0, 1fr); }
  .file-actions { grid-column: 1 / -1; }
}
</style>