<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import StatusSelect from './StatusSelect.vue'
import { VERSION_NOTES_MAX, VERSION_TITLE_MAX, type SongVersionInput, type SongVersionStatus } from '../types'

const props = withDefaults(defineProps<{ submitting?: boolean; serverErrors?: Record<string, string> }>(), {
  submitting: false,
  serverErrors: () => ({}),
})
const emit = defineEmits<{ submit: [input: SongVersionInput] }>()

const form = reactive<{ title: string; status: SongVersionStatus; notes: string }>({ title: '', status: 'Idea', notes: '' })
const submitted = ref(false)

const titleError = computed(() => {
  const title = form.title.trim()
  if (!title) return 'Le titre est obligatoire.'
  if (title.length > VERSION_TITLE_MAX) return `${VERSION_TITLE_MAX} caractères maximum.`
  return undefined
})

function onSubmit() {
  submitted.value = true
  if (titleError.value) return
  emit('submit', {
    title: form.title.trim(),
    status: form.status,
    notes: form.notes.trim() || null,
    lyrics: null,
    bpm: null,          // NOUVEAU : renseignés plus tard, dans le détail de la version
    key: null,          // NOUVEAU
  })
}
</script>

<template>
  <form novalidate class="card" @submit.prevent="onSubmit">
    <h3>Nouvelle version</h3>
    <div class="field">
      <label for="version-title">Titre *</label>
      <input id="version-title" v-model="form.title" :maxlength="VERSION_TITLE_MAX" placeholder="Ex. : maquette guitare-voix" />
      <p v-if="(submitted && titleError) || props.serverErrors.title" class="field-error">
        {{ (submitted && titleError) || props.serverErrors.title }}
      </p>
    </div>
    <div class="field">
      <label for="version-status">Statut</label>
      <StatusSelect id="version-status" v-model="form.status" />
    </div>
    <div class="field">
      <label for="version-notes">Notes</label>
      <textarea id="version-notes" v-model="form.notes" rows="3" :maxlength="VERSION_NOTES_MAX" />
    </div>
    <button type="submit" :disabled="submitting">{{ submitting ? 'Création…' : 'Créer la version' }}</button>
  </form>
</template>
