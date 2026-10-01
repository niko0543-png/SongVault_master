<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router'
import AudioPlayer from '@/features/player/components/AudioPlayer.vue'
</script>

<template>
  <header class="app-header">
    <RouterLink to="/songs" class="brand">🎸 SongVault</RouterLink>
  </header>
  <main class="app-main">
    <RouterView />   <!-- seules les pages changent… -->
  </main>
  <AudioPlayer />    <!-- …le lecteur, lui, reste monté -->
</template>

<style scoped>
.app-header { padding: 1rem 1.5rem; background: var(--surface); border-bottom: 1px solid var(--border); }
.brand { font-weight: 700; text-decoration: none; font-size: 1.2rem; }
.app-main { max-width: 900px; margin: 0 auto; padding: 1.5rem 1.5rem 7rem; }  /* place pour le lecteur */
</style>
