<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import SectionCard from '@/shared/components/SectionCard.vue'
import { getErrorMessage } from '@/shared/api/ApiError'
import { formatDate, formatFileSize } from '@/shared/utils/format'
import { accountApi, EXPORT_DOWNLOAD_URL } from '../api/accountApi'
import type { DataExport } from '../types'

const POLL_MS = 5000

const current = ref<DataExport | null>(null)
const error = ref<string | null>(null)
const isRequesting = ref(false)
let timer: ReturnType<typeof setTimeout> | undefined

const isPending = computed(() => current.value?.status === 'Pending')

/** Tant que l'archive se prépare, on redemande son état toutes les 5 secondes. */
function schedule() {
  clearTimeout(timer)
  if (isPending.value) timer = setTimeout(refresh, POLL_MS)
}

async function refresh() {
  try {
    current.value = await accountApi.latestExport()
    error.value = null
  } catch (e) {
    error.value = getErrorMessage(e)
  }
  schedule()
}

async function requestExport() {
  if (isRequesting.value) return
  isRequesting.value = true
  error.value = null
  try {
    current.value = await accountApi.requestExport()
    schedule()
  } catch (e) {
    error.value = getErrorMessage(e)
  } finally {
    isRequesting.value = false
  }
}

onMounted(refresh)
onBeforeUnmount(() => clearTimeout(timer))
</script>

<template>
  <SectionCard title="Vos données">
    <p class="muted">
      Une archive ZIP de votre compte, de vos groupes et de leurs morceaux, versions et fichiers.
      Un e-mail vous prévient quand elle est prête ; elle reste disponible 7 jours.
    </p>
    <p v-if="isPending" role="status">Préparation en cours…</p>
    <p v-else-if="current?.status === 'Ready'">
      Archive prête ({{ formatFileSize(current.sizeBytes ?? 0) }}), disponible jusqu’au {{ formatDate(current.expiresAt!) }}.
      <a :href="EXPORT_DOWNLOAD_URL" download>Télécharger l’archive</a>
    </p>
    <p v-else-if="current?.status === 'Failed'" class="field-error">
      La préparation a échoué. Réessayez ; si cela se reproduit, écrivez-nous.
    </p>
    <p v-if="error" class="field-error" role="alert">{{ error }}</p>
    <button type="button" :disabled="isPending || isRequesting" @click="requestExport">
      {{ current ? 'Préparer une nouvelle archive' : 'Exporter mes données' }}
    </button>
  </SectionCard>
</template>