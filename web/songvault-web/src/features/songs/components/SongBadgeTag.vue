<script setup lang="ts">
import type { SongBadge } from '../types'

/** Pastille « Nouveau » ou « Modifié » d'un morceau (#11) ; rien pour None. Le texte suffit, sans la couleur. */
defineProps<{ badge: SongBadge }>()

const labels: Record<Exclude<SongBadge, 'None'>, string> = { New: 'Nouveau', Updated: 'Modifié' }
const titles: Record<Exclude<SongBadge, 'None'>, string> = {
  New: "Pas encore ouvert, et modifié par un autre membre",
  Updated: 'Modifié par un autre membre depuis votre dernière visite',
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