<script setup lang="ts">
import { computed } from 'vue'
import { statusLabels, type SongVersionStatus } from '../types'

const props = defineProps<{ status: SongVersionStatus }>()

function toneFor(status: SongVersionStatus): string {
  switch (status) {
    case 'Idea': return 'idea'
    case 'Demo': return 'demo'
    case 'Arrangement': return 'arrangement'
    case 'Rehearsal': return 'rehearsal'
    case 'Studio': return 'studio'
    case 'Final': return 'final'
    default: {
      const unreachable: never = status
      throw new Error(`Statut non géré : ${String(unreachable)}`)
    }
  }
}

const tone = computed(() => toneFor(props.status))
</script>

<template>
  <span class="badge" :class="`badge--${tone}`" data-testid="status-badge">{{ statusLabels[status] }}</span>
</template>

<style scoped>
.badge { display: inline-block; padding: .1rem .6rem; border-radius: 999px; font-size: .85rem; font-weight: 600; }
.badge--idea { background: #eceff1; color: #37474f; }
.badge--demo { background: #e3f2fd; color: #0d47a1; }
.badge--arrangement { background: #ede7f6; color: #4527a0; }
.badge--rehearsal { background: #fff3e0; color: #8a3c00; }
.badge--studio { background: #e0f2f1; color: #00564d; }
.badge--final { background: #e8f5e9; color: #1b5e20; }
</style>
