<script setup lang="ts">
defineProps<{ title: string }>()
</script>

<template>
  <section class="card section-card">
    <header class="section-header">
      <h2>{{ title }}</h2>
      <slot name="actions" />
    </header>
    <slot />
  </section>
</template>

<style scoped>
.section-card { margin-bottom: 1rem; }
.section-header { display: flex; justify-content: space-between; align-items: center; gap: 1rem; }
.section-header h2 { margin: 0 0 .75rem; font-size: 1.1rem; }
</style>
