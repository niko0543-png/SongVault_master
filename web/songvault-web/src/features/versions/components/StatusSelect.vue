<script setup lang="ts">
import { SONG_VERSION_STATUSES, statusLabels, type SongVersionStatus } from '../types'

const model = defineModel<SongVersionStatus>({ required: true })
defineProps<{ id?: string; disabled?: boolean; label?: string }>()
</script>

<template>
  <select :id="id" v-model="model" :disabled="disabled" :aria-label="label">
    <option v-for="status in SONG_VERSION_STATUSES" :key="status" :value="status">
      {{ statusLabels[status] }}
    </option>
  </select>
</template>
