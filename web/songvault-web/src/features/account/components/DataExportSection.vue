<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import SectionCard from '@/shared/components/SectionCard.vue'
import { getErrorMessage } from '@/shared/api/ApiError'
import { formatDate, formatFileSize } from '@/shared/utils/format'
import { accountApi, EXPORT_DOWNLOAD_URL } from '../api/accountApi'
import type { DataExport } from '../types'
import { t } from '@/shared/i18n'

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
  <SectionCard :title="t('account.dataExport.title')">
    <p class="muted">
      {{ t('account.dataExport.intro') }}
    </p>
    <p v-if="isPending" role="status">{{ t('account.dataExport.preparing') }}</p>
    <p v-else-if="current?.status === 'Ready'">
      {{ t('account.dataExport.ready', { sizeBytes: formatFileSize(current.sizeBytes ?? 0), expiresAt: formatDate(current.expiresAt!) }) }}
      <a :href="EXPORT_DOWNLOAD_URL" download>{{ t('account.dataExport.download') }}</a>
    </p>
    <p v-else-if="current?.status === 'Failed'" class="field-error">
      {{ t('account.dataExport.failed') }}
    </p>
    <p v-if="error" class="field-error" role="alert">{{ error }}</p>
    <button type="button" :disabled="isPending || isRequesting" @click="requestExport">
      {{ current ? t('account.dataExport.prepareNew') : t('account.dataExport.export') }}
    </button>
  </SectionCard>
</template>