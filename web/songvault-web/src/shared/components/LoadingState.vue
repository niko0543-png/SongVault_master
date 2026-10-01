<script setup lang="ts">
withDefaults(defineProps<{ message?: string }>(), { message: 'Chargement…' })
</script>

<template>
  <p class="muted" role="status" aria-live="polite" data-testid="loading-state">{{ message }}</p>
</template>
