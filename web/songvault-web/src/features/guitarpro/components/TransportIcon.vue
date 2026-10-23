<script setup lang="ts">
/** #34 : icônes de lecture dessinées ici (pas de paquet d'icônes) ; le libellé est sur le bouton (aria-label). */
defineProps<{ name: 'play' | 'pause' | 'stop' }>()
</script>

<template>
  <svg class="transport-icon" viewBox="0 0 16 16" width="16" height="16" aria-hidden="true" focusable="false">
    <path v-if="name === 'play'" d="M4 2.5v11l9-5.5z" fill="currentColor" />
    <path v-else-if="name === 'pause'" d="M4 2.5h3v11H4zM9 2.5h3v11H9z" fill="currentColor" />
    <path v-else d="M3.5 3.5h9v9h-9z" fill="currentColor" />
  </svg>
</template>

<style scoped>
.transport-icon { display: block; }
</style>
