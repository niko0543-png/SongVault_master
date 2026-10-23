<script setup lang="ts">
import { SONG_VERSION_STATUSES, statusLabels, type SongVersionStatus } from '@/features/versions/types'
import { t } from '@/shared/i18n'

const search = defineModel<string>('search', { required: true })
const status = defineModel<SongVersionStatus | ''>('status', { required: true })
</script>

<template>
  <div class="search-bar" role="search">
    <label for="song-search" class="visually-hidden">{{ t('songs.search.label') }}</label>
    <input id="song-search" v-model="search" type="search" :placeholder="t('songs.search.placeholder')" maxlength="100" />
    <label for="song-status" class="visually-hidden">{{ t('songs.search.statusLabel') }}</label>
    <select id="song-status" v-model="status">
      <option value="">{{ t('songs.search.allStatuses') }}</option>
      <option v-for="s in SONG_VERSION_STATUSES" :key="s" :value="s">{{ statusLabels[s] }}</option>
    </select>
  </div>
</template>

<style scoped>
.search-bar { display: grid; grid-template-columns: 1fr auto; gap: .5rem; margin-bottom: 1rem; }
.search-bar select { width: auto; }
.visually-hidden { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); }
</style>