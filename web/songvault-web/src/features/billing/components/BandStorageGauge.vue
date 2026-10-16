<script setup lang="ts">
import { ref, watch } from 'vue'
import { billingApi } from '../api/billingApi'
import type { BandUsage } from '../types'
import StorageGauge from './StorageGauge.vue'
import { useBandStore } from '@/features/bands/stores/bandStore'

// Jauge du groupe actif (liste des morceaux). Une erreur la cache : elle ne doit jamais gêner la page
const bands = useBandStore()
const usage = ref<BandUsage | null>(null)

watch(() => bands.activeId, async (id) => {
  usage.value = null
  if (!id) return
  try {
    usage.value = (await billingApi.usage()) ?? null
  } catch {
    usage.value = null
  }
}, { immediate: true })
</script>

<template>
  <StorageGauge v-if="usage" :usage="usage" :billing-link="bands.can('admin')" />
</template>