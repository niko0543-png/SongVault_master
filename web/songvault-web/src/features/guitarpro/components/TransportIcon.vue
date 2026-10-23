<script setup lang="ts">
/**
 * #34 : icônes de lecture dessinées ici (pas de paquet d'icônes) ; le libellé est sur le bouton (aria-label).
 * #35 : celles de la barre façon Guitar Pro : début, mesure précédente ou suivante, fin, boucle, métronome,
 * décompte, transposition (diapason) et menu.
 */
export type TransportIconName =
  | 'play' | 'pause' | 'stop' | 'first' | 'prev' | 'next' | 'last'
  | 'loop' | 'metronome' | 'countIn' | 'fork' | 'menu'
defineProps<{ name: TransportIconName }>()
</script>

<template>
  <svg class="transport-icon" viewBox="0 0 16 16" width="16" height="16" aria-hidden="true" focusable="false">
    <path v-if="name === 'play'" d="M4 2.5v11l9-5.5z" fill="currentColor" />
    <path v-else-if="name === 'pause'" d="M4 2.5h3v11H4zM9 2.5h3v11H9z" fill="currentColor" />
    <path v-else-if="name === 'stop'" d="M3.5 3.5h9v9h-9z" fill="currentColor" />
    <path v-else-if="name === 'first'" d="M3 3h2v10H3zM13 3v10L6 8z" fill="currentColor" />
    <path v-else-if="name === 'prev'" d="M8 3v10L2 8zM14 3v10L8 8z" fill="currentColor" />
    <path v-else-if="name === 'next'" d="M2 3v10l6-5zM8 3v10l6-5z" fill="currentColor" />
    <path v-else-if="name === 'last'" d="M3 3v10l7-5zM11 3h2v10h-2z" fill="currentColor" />
    <g v-else-if="name === 'loop'" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round">
      <path d="M4 5.5h7a3 3 0 0 1 0 6H5a3 3 0 0 1-1.6-5.5" />
      <path d="M6 3.5 4 5.5l2 2" />
    </g>
    <g v-else-if="name === 'metronome'" fill="currentColor">
      <path d="M6 1.5h4l3 13H3z" opacity=".35" />
      <path d="M7.4 10.5 12.6 2.6l.9.6-5.2 7.9z" />
      <path d="M3 13.5h10v1H3z" />
    </g>
    <g v-else-if="name === 'countIn'" fill="currentColor">
      <path d="M3.5 1.5h9v1.5h-1v1.8L9 8l2.5 3.2V13h1v1.5h-9V13h1v-1.8L7 8 4.5 4.8V3h-1z" opacity=".35" />
      <path d="M5.5 13h5v-1.3L8 9 5.5 11.7zM5.5 3h5v1.3L8 7 5.5 4.3z" />
    </g>
    <g v-else-if="name === 'fork'" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round">
      <path d="M5 2v5a3 3 0 0 0 6 0V2" />
      <path d="M8 10v4.5" />
    </g>
    <g v-else fill="currentColor">
      <circle cx="8" cy="3.5" r="1.4" /><circle cx="8" cy="8" r="1.4" /><circle cx="8" cy="12.5" r="1.4" />
    </g>
  </svg>
</template>

<style scoped>
.transport-icon { display: block; }
</style>
