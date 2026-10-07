<script setup lang="ts">
import { SONG_VERSION_STATUSES, statusLabels, type SongVersionStatus } from '@/features/versions/types'

const search = defineModel<string>('search', { required: true })
const status = defineModel<SongVersionStatus | ''>('status', { required: true })
</script>

<template>
  <div class="search-bar" role="search">
    <label for="song-search" class="visually-hidden">Rechercher un morceau</label>
    <input id="song-search" v-model="search" type="search" placeholder="Rechercher par titre ou artiste…" maxlength="100" />
    <label for="song-status" class="visually-hidden">Filtrer par statut</label>
    <select id="song-status" v-model="status">
      <option value="">Tous les statuts</option>
      <option v-for="s in SONG_VERSION_STATUSES" :key="s" :value="s">{{ statusLabels[s] }}</option>
    </select>
  </div>
</template>

<style scoped>
.search-bar { display: grid; grid-template-columns: 1fr auto; gap: .5rem; margin-bottom: 1rem; }
.search-bar select { width: auto; }
.visually-hidden { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); }
</style>