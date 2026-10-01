<script setup lang="ts">
defineProps<{ message: string }>()
defineEmits<{ retry: [] }>()
</script>

<template>
  <div class="card" role="alert" data-testid="error-state">
    <p>{{ message }}</p>
    <button type="button" class="secondary" @click="$emit('retry')">Réessayer</button>
  </div>
</template>
