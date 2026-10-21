<script setup lang="ts">
import type { SongBadge } from '../types'
import { t } from '@/shared/i18n'

/** Pastille « Nouveau » ou « Modifié » d'un morceau (#11) ; rien pour None. Le texte suffit, sans la couleur. */
defineProps<{ badge: SongBadge }>()

const labels: Record<Exclude<SongBadge, 'None'>, string> = { New: t('songs.badge.new'), Updated: t('songs.badge.updated') }
const titles: Record<Exclude<SongBadge, 'None'>, string> = {
  New: t('songs.badge.newHelp'),
  Updated: t('songs.badge.updatedHelp'),
}
</script>

<template>
  <span v-if="badge !== 'None'" class="song-badge" :class="`song-badge--${badge.toLowerCase()}`"
        :title="titles[badge]" data-testid="song-badge">{{ labels[badge] }}</span>
</template>

<style scoped>
.song-badge { display: inline-block; padding: .05rem .5rem; border-radius: 999px; font-size: .8rem; font-weight: 600; }
.song-badge--new { background: #e3f2fd; color: #0d47a1; }
.song-badge--updated { background: #fff3e0; color: #8a3c00; }
</style>