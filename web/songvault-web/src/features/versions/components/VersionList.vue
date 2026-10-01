<script setup lang="ts">
import type { SongVersionSummary } from '../types'
import { statusLabels } from '../types'
import { formatDate } from '@/shared/utils/format'

defineProps<{ versions: SongVersionSummary[] }>()
</script>

<template>
  <p v-if="versions.length === 0" class="muted">Aucune version pour l'instant.</p>
  <ol v-else class="version-list">
    <li v-for="version in versions" :key="version.id" class="card">
      <strong>v{{ version.number }}</strong> — {{ version.title }}
      <span class="status" :data-status="version.status">{{ statusLabels[version.status] }}</span>
      <br />
      <small class="muted">{{ formatDate(version.createdAt) }}</small>
    </li>
  </ol>
</template>

<style scoped>
.version-list { list-style: none; padding: 0; display: grid; gap: .5rem; }
.status { margin-left: .5rem; padding: .1rem .5rem; border-radius: 999px; background: var(--bg); font-size: .85rem; }
</style>
