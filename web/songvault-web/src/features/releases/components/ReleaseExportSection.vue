<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import SectionCard from '@/shared/components/SectionCard.vue'
import { getErrorMessage } from '@/shared/api/ApiError'
import { formatDate, formatFileSize } from '@/shared/utils/format'
import { releaseExportsApi } from '../api/releaseExportsApi'
import type { ReleaseExport } from '../types'

const POLL_MS = 5000

const props = defineProps<{ releaseId: string; canWrite: boolean }>()

const current = ref<ReleaseExport | null>(null)
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
    current.value = await releaseExportsApi.latest(props.releaseId)
    error.value = null
  } catch (e) {
    error.value = getErrorMessage(e)
  }
  schedule()
}

async function requestArchive() {
  if (isRequesting.value) return
  isRequesting.value = true
  error.value = null
  try {
    current.value = await releaseExportsApi.request(props.releaseId)
    schedule()
  } catch (e) {
    error.value = getErrorMessage(e)
  } finally {
    isRequesting.value = false
  }
}

watch(() => props.releaseId, () => {
  current.value = null
  void refresh()
}, { immediate: true })
onBeforeUnmount(() => clearTimeout(timer))
</script>

<template>
  <SectionCard title="Archive pour le distributeur">
    <p class="muted">
      Un ZIP avec les fichiers audio des versions choisies, les pistes séparées, les paroles, les crédits et la pochette.
      Un e-mail prévient quand il est prêt ; il reste disponible 24 heures.
    </p>
    <p v-if="isPending" role="status">Préparation en cours…</p>
    <p v-else-if="current?.status === 'Ready'">
      Archive prête ({{ formatFileSize(current.sizeBytes ?? 0) }}), disponible jusqu’au {{ formatDate(current.expiresAt!) }}.
      <a :href="releaseExportsApi.archiveUrl(releaseId, current.id)" download>Télécharger l’archive</a>
    </p>
    <p v-else-if="current?.status === 'Failed'" class="field-error">
      La préparation a échoué. Réessayez ; si cela se reproduit, écrivez-nous.
    </p>
    <p v-if="error" class="field-error" role="alert">{{ error }}</p>
    <button v-if="canWrite" type="button" :disabled="isPending || isRequesting" @click="requestArchive">
      {{ current ? 'Préparer une nouvelle archive' : 'Préparer l’archive' }}
    </button>
  </SectionCard>
</template>