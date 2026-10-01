<script setup lang="ts">
defineProps<{ message: string }>()
</script>

<template>
  <div class="card muted" data-testid="empty-state">
    <p>{{ message }}</p>
    <slot />   <!-- contenu optionnel fourni par le parent, ex. un bouton "Créer" -->
  </div>
</template>
