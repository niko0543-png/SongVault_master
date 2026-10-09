<script setup lang="ts">
import { useToast } from '../composables/useToast'

const { toasts, dismiss } = useToast()
</script>

<template>
  <div class="toast-host">
    <div
      v-for="toast in toasts"
      :key="toast.id"
      class="toast"
      :class="`toast--${toast.kind}`"
      :role="toast.kind === 'error' ? 'alert' : 'status'"
      data-testid="toast"
    >
      <span>{{ toast.message }}</span>
      <button type="button" class="toast-close" aria-label="Fermer la notification" @click="dismiss(toast.id)">×</button>
    </div>
  </div>
</template>

<style scoped>
.toast-host { position: fixed; right: 1rem; bottom: 6rem; display: grid; gap: .5rem; z-index: 50; max-width: min(24rem, calc(100vw - 2rem)); }
.toast { display: flex; align-items: center; gap: .75rem; padding: .75rem 1rem; border-radius: var(--radius);
         background: var(--surface); border: 1px solid var(--border); box-shadow: 0 4px 16px rgb(0 0 0 / .12); }
.toast--success { border-left: 4px solid #1b7a3d; }
.toast--error { border-left: 4px solid var(--danger); }
.toast--info { border-left: 4px solid var(--primary); }
.toast-close { margin-left: auto; background: none; border: none; color: var(--muted); font-size: 1.25rem; padding: 0 .25rem; }
</style>