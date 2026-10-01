src/features/versions/components/VersionTimeline.vue :

<script setup lang="ts">
import type { SongVersionStatus, SongVersionSummary } from '../types'
import StatusBadge from './StatusBadge.vue'
import StatusSelect from './StatusSelect.vue'
import { formatDate } from '@/shared/utils/format'

defineProps<{ versions: SongVersionSummary[]; savingId?: string | null }>()
const emit = defineEmits<{ changeStatus: [versionId: string, status: SongVersionStatus] }>()
</script>

<template>
  <p v-if="versions.length === 0" class="muted">Aucune version pour l'instant. Créez la première ci-dessous.</p>
  <ol v-else class="timeline">
    <li v-for="version in versions" :key="version.id" class="card timeline-item" data-testid="version-item">
      <span class="number">v{{ version.number }}</span>
      <div class="content">
        <strong>{{ version.title }}</strong>
        <small class="muted">{{ formatDate(version.createdAt) }}</small>
      </div>
      <StatusBadge :status="version.status" />
      <!-- :model-value + @update plutôt que v-model : la valeur affichée ne change qu'après la réponse serveur -->
      <StatusSelect
        :model-value="version.status"
        :disabled="savingId === version.id"
        :label="`Statut de la version ${version.number}`"
        @update:model-value="(status) => emit('changeStatus', version.id, status)"
      />
    </li>
  </ol>
</template>

<style scoped>
.timeline { list-style: none; padding: 0; display: grid; gap: .5rem; }
.timeline-item { display: grid; grid-template-columns: auto 1fr auto auto; gap: .75rem; align-items: center; }
.number { font-weight: 700; font-size: 1.2rem; color: var(--primary); }
.content { display: grid; }
.timeline-item select { width: auto; }
</style>
