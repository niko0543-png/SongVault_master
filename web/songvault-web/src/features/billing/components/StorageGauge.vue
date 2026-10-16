<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import type { BandUsage } from '../types'
import { usageRatio } from '../format'
import { formatFileSize } from '@/shared/utils/format'

const props = defineProps<{
  usage: BandUsage
  /** Lien vers la page Facturation : pour les propriétaires seulement. */
  billingLink?: boolean
}>()

const ratio = computed(() => usageRatio(props.usage.usedBytes, props.usage.quotaBytes))
const level = computed(() => (ratio.value >= 1 ? 'full' : ratio.value >= 0.8 ? 'high' : 'ok'))
</script>

<template>
  <div class="storage-gauge" :class="`storage-gauge--${level}`" data-testid="storage-gauge">
    <label for="storage-meter">
      Stockage : {{ formatFileSize(usage.usedBytes) }} sur {{ formatFileSize(usage.quotaBytes) }}
      <span class="muted">(formule {{ usage.planName }})</span>
    </label>
    <meter id="storage-meter" min="0" max="1" low="0.8" high="0.99" optimum="0" :value="ratio" />
    <p v-if="level !== 'ok'" class="storage-hint">
      {{ level === 'full'
        ? 'Stockage plein : les nouveaux fichiers sont refusés.'
        : 'Stockage presque plein.' }}
      <RouterLink v-if="billingLink" :to="{ name: 'billing' }">Changer de formule</RouterLink>
      <template v-else>Le propriétaire du groupe peut changer de formule.</template>
    </p>
  </div>
</template>

<style scoped>
.storage-gauge { margin: .5rem 0 1rem; font-size: .9rem; }
.storage-gauge meter { display: block; width: 100%; max-width: 24rem; height: .75rem; }
.storage-hint { margin: .25rem 0 0; }
.storage-gauge--full .storage-hint { color: var(--danger, #b00020); }
</style>