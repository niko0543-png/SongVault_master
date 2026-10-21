<script setup lang="ts">
import { computed, ref, useTemplateRef } from 'vue'
import { filesApi } from '../api/filesApi'
import { useFilePolicy } from '../composables/useFilePolicy'
import { validateFile } from '../validation'
import type { SongFile } from '../types'
import { useAsyncAction } from '@/shared/composables/useAsyncAction'
import { ApiError, getErrorMessage } from '@/shared/api/ApiError'
import { RouterLink } from 'vue-router'
import { useBandStore } from '@/features/bands/stores/bandStore'
import { t } from '@/shared/i18n'

const props = defineProps<{ songId: string; versionId: string }>()
const emit = defineEmits<{ uploaded: [file: SongFile] }>()

const { policy, error: policyError } = useFilePolicy()
const fileInput = useTemplateRef<HTMLInputElement>('file-input')
const message = ref<string | null>(null)
const currentName = ref<string | null>(null)
const isDragging = ref(false)
const progress = ref<number | null>(null)                    // de 0 à 1 ; null tant que l'envoi n'a pas commencé

const bands = useBandStore()
const upload = useAsyncAction((file: File) =>
  filesApi.upload(props.songId, props.versionId, file, (ratio) => { progress.value = ratio }))
/** #16 : le dernier refus vient du quota ; le propriétaire peut changer de formule. */
const isQuotaError = computed(() => upload.error.value instanceof ApiError && upload.error.value.status === 402)
const accept = computed(() => policy.value?.allowedExtensions.join(',') ?? '')

function serverMessage(error: unknown): string {
  if (error instanceof ApiError && error.status === 413) return t('files.uploader.tooLarge')
  if (error instanceof ApiError && error.status === 400) return error.message   // ex. « Type de fichier non autorisé… »
  if (error instanceof ApiError && error.status === 402) return error.message   // #16 : « Stockage du groupe plein… »
  if (error instanceof ApiError && error.status === 403 && error.message === t('files.upload.refused'))
    return t('files.uploader.storageRefused')
  return getErrorMessage(error)
}

async function handle(file: File | undefined) {
  if (!file || !policy.value) return
  message.value = validateFile(file, policy.value)           // 1. confort : refus immédiat
  if (message.value) return

  currentName.value = file.name
  progress.value = null
  const uploaded = await upload.run(file)                    // 2. le serveur décide
  currentName.value = null
  progress.value = null
  if (uploaded) emit('uploaded', uploaded)
  else message.value = serverMessage(upload.error.value)

  if (fileInput.value) fileInput.value.value = ''            // permet de renvoyer le même fichier
}

function onChange(event: Event) {
  void handle((event.target as HTMLInputElement).files?.[0])
}

function onDrop(event: DragEvent) {
  isDragging.value = false
  void handle(event.dataTransfer?.files[0])
}
</script>

<template>
  <div
    class="dropzone"
    :class="{ 'dropzone--active': isDragging }"
    data-testid="file-uploader"
    @dragover.prevent="isDragging = true"
    @dragleave="isDragging = false"
    @drop.prevent="onDrop"
  >
    <p v-if="policyError" class="field-error">{{ t('files.uploader.policyFailed') }}</p>
    <template v-else>
      <label for="file-input" class="secondary-button">{{ t('files.uploader.choose') }}</label>
      <input id="file-input" ref="file-input" type="file" class="visually-hidden"
             :accept="accept" :disabled="!policy || upload.isLoading.value" @change="onChange" />
      <span class="muted"> {{ t('files.uploader.orDrop') }}</span>
      <p v-if="policy" class="muted"><small>{{ policy.allowedExtensions.join(', ') }}</small></p>
    </template>

       <div v-if="upload.isLoading.value" role="status">
      <!-- value absent (null) : barre indéterminée, le temps de l'annonce et de la confirmation -->
      <progress :aria-label="t('files.uploader.progress')" max="1" :value="progress ?? undefined" />
      {{ t('files.uploader.uploading', { currentName }) }}
      <span v-if="progress !== null" data-testid="upload-progress">{{ Math.round(progress * 100) }} %</span>
    </div>
      <p v-if="message" class="field-error" role="alert" data-testid="upload-error">
        {{ message }}
        <RouterLink v-if="isQuotaError && bands.can('admin')" :to="{ name: 'billing' }">{{ t('files.uploader.seePlans') }}</RouterLink>
      </p>
  </div>
</template>

<style scoped>
.dropzone { border: 2px dashed var(--border); border-radius: var(--radius); padding: 1rem; margin-top: 1rem; }
.dropzone--active { border-color: var(--primary); background: #f1edff; }
.secondary-button { display: inline-block; padding: .5rem 1rem; border: 1px solid var(--primary);
                    border-radius: var(--radius); color: var(--primary); cursor: pointer; }
.visually-hidden { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); }
</style>
