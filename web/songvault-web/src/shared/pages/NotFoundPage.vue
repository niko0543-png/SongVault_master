<script setup lang="ts">
import { RouterLink } from 'vue-router'
</script>

<template>
  <section class="card">
    <h1>Page introuvable</h1>
    <p>L'adresse demandée n'existe pas.</p>
    <RouterLink to="/songs">Retour aux morceaux</RouterLink>
  </section>
</template>
