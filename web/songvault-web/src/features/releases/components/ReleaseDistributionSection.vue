<script setup lang="ts">
import { reactive, ref } from 'vue'
import { releasesApi } from '../api/releasesApi'
import { RELEASE_LABEL_MAX, UPC_INPUT_MAX, type Release } from '../types'
import StreamingLinksEditor from './StreamingLinksEditor.vue'
import StreamingLinkList from './StreamingLinkList.vue'
import SectionCard from '@/shared/components/SectionCard.vue'
import { getErrorMessage } from '@/shared/api/ApiError'
import { t } from '@/shared/i18n'

/** #38 : label, code-barres et liens d'écoute d'une sortie, sur sa page. */
const props = defineProps<{ release: Release; canWrite: boolean }>()
const emit = defineEmits<{ saved: [release: Release] }>()

const isEditing = ref(false)
const isSaving = ref(false)
const error = ref<string | null>(null)
const form = reactive({ label: '', upc: '', links: [] as string[] })

function onEdit() {
  form.label = props.release.label ?? ''
  form.upc = props.release.upc ?? ''
  form.links = props.release.streamingLinks.length ? props.release.streamingLinks.map((l) => l.url) : ['']
  error.value = null
  isEditing.value = true
}

async function onSave() {
  isSaving.value = true
  error.value = null
  try {
    const saved = await releasesApi.setDistribution(props.release.id, {
      label: form.label.trim() || null,
      upc: form.upc.trim() || null,
      streamingLinks: form.links.map((l) => l.trim()).filter((l) => l !== ''),
    })
    emit('saved', saved)
    isEditing.value = false
  } catch (e) {
    error.value = getErrorMessage(e)
  } finally {
    isSaving.value = false
  }
}
</script>

<template>
  <SectionCard :title="t('releases.distribution.title')" data-testid="release-distribution">
    <template v-if="canWrite && !isEditing" #actions>
      <button type="button" class="secondary" @click="onEdit">{{ t('releases.detail.edit') }}</button>
    </template>

    <form v-if="isEditing" class="distribution" novalidate @submit.prevent="onSave">
      <div class="row">
        <div class="field">
          <label for="rd-label">{{ t('releases.distribution.label') }}</label>
          <input id="rd-label" v-model="form.label" :maxlength="RELEASE_LABEL_MAX" autocomplete="off" />
        </div>
        <div class="field">
          <label for="rd-upc">{{ t('releases.distribution.upc') }}</label>
          <input id="rd-upc" v-model="form.upc" :maxlength="UPC_INPUT_MAX" inputmode="numeric" autocomplete="off" />
        </div>
      </div>
      <StreamingLinksEditor v-model="form.links" id-prefix="rd" />
      <p v-if="error" class="field-error" role="alert">{{ error }}</p>
      <div class="buttons">
        <button type="button" class="secondary" :disabled="isSaving" @click="isEditing = false">{{ t('common.actions.cancel') }}</button>
        <button type="submit" :disabled="isSaving">{{ isSaving ? t('common.actions.saving') : t('common.actions.save') }}</button>
      </div>
    </form>

    <template v-else>
      <dl v-if="release.label || release.upc" class="facts">
        <template v-if="release.label"><dt>{{ t('releases.distribution.label') }}</dt><dd>{{ release.label }}</dd></template>
        <template v-if="release.upc"><dt>{{ t('releases.distribution.upc') }}</dt><dd>{{ release.upc }}</dd></template>
      </dl>
      <StreamingLinkList :links="release.streamingLinks" />
      <p v-if="!release.label && !release.upc && release.streamingLinks.length === 0" class="muted">
        {{ t('releases.distribution.empty') }}
      </p>
    </template>
  </SectionCard>
</template>

<style scoped>
.distribution { display: grid; gap: .75rem; }
.row { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
@media (max-width: 600px) { .row { grid-template-columns: 1fr; } }
.buttons { display: flex; gap: .75rem; justify-content: flex-end; }
.facts { display: grid; grid-template-columns: max-content 1fr; gap: .25rem 1rem; margin: 0 0 .5rem; }
.facts dt { color: var(--muted); }
.facts dd { margin: 0; }
</style>
