<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import type { SongInput } from '../types'
import { SONG_ARTIST_MAX, SONG_DESCRIPTION_MAX, SONG_TITLE_MAX } from '../rules'

type Field = 'title' | 'artist' | 'description'

const props = withDefaults(
  defineProps<{
    initial?: SongInput
    submitting?: boolean
    serverErrors?: Record<string, string>
    submitLabel?: string
  }>(),
  { submitting: false, serverErrors: () => ({}), submitLabel: 'Enregistrer' },
)

const emit = defineEmits<{ submit: [input: SongInput]; cancel: [] }>()

// Copie LOCALE des valeurs initiales : le formulaire ne modifie jamais la prop
const form = reactive({
  title: props.initial?.title ?? '',
  artist: props.initial?.artist ?? '',
  description: props.initial?.description ?? '',
})
const submitted = ref(false)

const clientErrors = computed(() => {
  const errors: Partial<Record<Field, string>> = {}
  const title = form.title.trim()
  if (!title) errors.title = 'Le titre est obligatoire.'
  else if (title.length > SONG_TITLE_MAX) errors.title = `${SONG_TITLE_MAX} caractères maximum.`
  if (form.artist.trim().length > SONG_ARTIST_MAX) errors.artist = `${SONG_ARTIST_MAX} caractères maximum.`
  if (form.description.trim().length > SONG_DESCRIPTION_MAX)
    errors.description = `${SONG_DESCRIPTION_MAX} caractères maximum.`
  return errors
})

const isValid = computed(() => Object.keys(clientErrors.value).length === 0)

/** Erreur client (après une première tentative), sinon erreur serveur. */
function errorFor(field: Field): string | undefined {
  return (submitted.value ? clientErrors.value[field] : undefined) ?? props.serverErrors[field]
}

function onSubmit() {
  submitted.value = true
  if (!isValid.value) return           // ← commentez cette ligne pour tester l'erreur serveur (étape 14.7)
  emit('submit', {
    title: form.title.trim(),
    artist: form.artist.trim() || null,
    description: form.description.trim() || null,
  })
}
</script>

<template>
  <form novalidate class="card" @submit.prevent="onSubmit">
    <div class="field">
      <label for="song-title">Titre *</label>
      <input
        id="song-title"
        v-model="form.title"
        data-testid="title-input"
        :maxlength="SONG_TITLE_MAX"
        :aria-invalid="!!errorFor('title')"
        aria-describedby="song-title-error"
      />
      <p v-if="errorFor('title')" id="song-title-error" class="field-error" data-testid="title-error">
        {{ errorFor('title') }}
      </p>
    </div>

    <div class="field">
      <label for="song-artist">Artiste</label>
      <input id="song-artist" v-model="form.artist" data-testid="artist-input" :maxlength="SONG_ARTIST_MAX" />
      <p v-if="errorFor('artist')" class="field-error">{{ errorFor('artist') }}</p>
    </div>

    <div class="field">
      <label for="song-description">Description</label>
      <textarea id="song-description" v-model="form.description" rows="4" :maxlength="SONG_DESCRIPTION_MAX" />
      <p v-if="errorFor('description')" class="field-error">{{ errorFor('description') }}</p>
    </div>

    <div class="form-actions">
      <button type="submit" :disabled="submitting">{{ submitting ? 'Envoi…' : submitLabel }}</button>
      <button type="button" class="secondary" @click="emit('cancel')">Annuler</button>
    </div>
  </form>
</template>

<style scoped>
.form-actions { display: flex; gap: .5rem; }
</style>
