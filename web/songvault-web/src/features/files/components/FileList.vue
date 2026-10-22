<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { isReadable, type SongFile, type SongFileType } from '../types'
import { filesApi } from '../api/filesApi'
import FileWaveform from './FileWaveform.vue'
import { formatDate, formatFileSize } from '@/shared/utils/format'
import { t } from '@/shared/i18n'

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

// #32 : une tablature s'ouvre sur la page du lecteur (TablaturePage), qui charge alphaTab à la demande (ADR 0038).
// Le groupe de l'adresse est repris de la page en cours par le routeur.
</script>

<template>
  <p v-if="files.length === 0" class="muted">{{ t('files.list.empty') }}</p>
  <ul v-else class="file-list">
    <li v-for="file in files" :key="file.id" class="file-item" data-testid="file-item">
      <span aria-hidden="true">{{ icons[file.fileType] }}</span>
      <div class="file-info">
        <span>{{ file.originalFileName }}</span>
        <small class="muted">{{ formatFileSize(file.sizeBytes) }} · {{ formatDate(file.uploadedAt) }}</small>
        <small v-if="file.scan === 'Pending'" class="muted" role="status">{{ t('files.list.scanning') }}</small>
        <small v-else-if="file.scan === 'Infected'" class="preview-failed" data-testid="scan-infected">
          {{ t('files.list.blocked', { scanThreat: file.scanThreat }) }}
        </small>
        <small v-else-if="file.scan === 'Failed'" class="preview-failed">
          {{ t('files.list.scanFailed') }}
          <button v-if="!readonly" type="button" class="link" :disabled="busyId === file.id"
                  @click="$emit('retryScan', file)">{{ t('files.list.rescan') }}</button>
        </small>
        <small v-else-if="file.preview === 'Pending'" class="muted" role="status">{{ t('files.list.previewPending') }}</small>
        <small v-else-if="file.preview === 'Failed'" class="preview-failed">
          {{ t('files.list.previewFailed') }}
          <button v-if="!readonly" type="button" class="link" :disabled="busyId === file.id"
                  @click="$emit('retry', file)">{{ t('common.actions.retry') }}</button>
        </small>
      </div>
      <div class="file-actions">
        <template v-if="isReadable(file)">
          <button v-if="file.fileType === 'Audio'" type="button" class="secondary" @click="$emit('play', file)">{{ t('files.list.play') }}</button>
          <RouterLink v-if="file.fileType === 'Tablature'" class="tab-link" data-testid="show-tablature"
                      :to="{ name: 'tablature', params: { songId, versionId, fileId: file.id } }">
            {{ t('files.list.showTablature') }}
          </RouterLink>
          <a :href="filesApi.contentUrl(songId, versionId, file.id, true)" download>{{ t('files.list.download') }}</a>
        </template>
        <button v-if="!readonly" type="button" class="secondary" :disabled="busyId === file.id" @click="$emit('delete', file)">{{ t('common.actions.delete') }}</button>
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
/* #32 : un lien vers la page du lecteur, à l'allure d'un bouton secondaire (clic molette : nouvel onglet) */
.tab-link { padding: .5rem 1rem; border: 1px solid var(--primary); border-radius: var(--radius); text-decoration: none; }
.preview-failed { color: var(--danger); }
.link { background: none; border: 0; padding: 0; color: var(--primary); text-decoration: underline; cursor: pointer; font: inherit; }
/* #14 : téléphone. Les boutons passent sous le nom du fichier, qui garde toute la largeur */
@media (max-width: 600px) {
  .file-item { grid-template-columns: auto minmax(0, 1fr); }
  .file-actions { grid-column: 1 / -1; }
}
</style>