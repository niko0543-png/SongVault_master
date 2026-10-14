<script setup lang="ts">
import { reactive } from 'vue'
import { RELEASE_TITLE_MAX, RELEASE_TYPES, releaseTypeLabels, type ReleaseInput } from '../types'

/** Titre, type et date d'une sortie : création (liste) et modification (détail). */
const props = defineProps<{
  initial?: ReleaseInput
  submitLabel: string
  submitting?: boolean
  error?: string | null
}>()
const emit = defineEmits<{ submit: [input: ReleaseInput]; cancel: [] }>()

const form = reactive({
  title: props.initial?.title ?? '',
  type: props.initial?.type ?? 'Single',
  releaseDate: props.initial?.releaseDate ?? '',
})

function onSubmit() {
  const title = form.title.trim()
  if (!title) return
  emit('submit', { title, type: form.type, releaseDate: form.releaseDate || null })
}
</script>

<template>
  <form class="release-form" @submit.prevent="onSubmit">
    <div class="field">
      <label for="release-title">Titre</label>
      <input id="release-title" v-model="form.title" required :maxlength="RELEASE_TITLE_MAX" autocomplete="off" />
    </div>
    <div class="field">
      <label for="release-type">Type</label>
      <select id="release-type" v-model="form.type">
        <option v-for="type in RELEASE_TYPES" :key="type" :value="type">{{ releaseTypeLabels[type] }}</option>
      </select>
    </div>
    <div class="field">
      <label for="release-date">Date de sortie <small class="muted">(facultative)</small></label>
      <input id="release-date" v-model="form.releaseDate" type="date" />
    </div>
    <p v-if="error" class="field-error" role="alert">{{ error }}</p>
    <div class="buttons">
      <button type="submit" :disabled="submitting">{{ submitLabel }}</button>
      <button v-if="initial" type="button" class="secondary" @click="emit('cancel')">Annuler</button>
    </div>
  </form>
</template>

<style scoped>
.release-form { display: grid; grid-template-columns: 2fr 1fr 1fr; gap: 0 .75rem; align-items: end; }
.release-form .field-error, .release-form .buttons { grid-column: 1 / -1; }
.buttons { display: flex; gap: .5rem; }
@media (max-width: 640px) { .release-form { grid-template-columns: 1fr; } }
</style>