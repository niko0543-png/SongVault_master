<script setup lang="ts">
import { computed, ref } from 'vue'
import ChordSheet from './ChordSheet.vue'
import { hasChords, transposition } from '../chordpro'
import { parseKey, transposeKey } from '../chords'
import { VERSION_LYRICS_MAX } from '@/features/versions/types'
import { useConfirm } from '@/shared/composables/useConfirm'

/**
 * #25 : paroles d'une version, lues avec leurs accords ou modifiées en texte (ADR 0032).
 * La transposition ne change que l'affichage : modelValue n'est réécrit que par « Enregistrer dans cette tonalité ».
 * La page remonte le panneau à chaque version (:key), ce qui remet le décalage à zéro.
 */
const props = defineProps<{
  modelValue: string
  musicalKey: string
  canWrite: boolean
  /** Le formulaire a des modifications non enregistrées : on n'enregistre pas une transposition par-dessus. */
  dirty: boolean
}>()
const emit = defineEmits<{
  'update:modelValue': [lyrics: string]
  'save-transposed': [lyrics: string, key: string | null]
}>()
const { confirm } = useConfirm()

const mode = ref<'read' | 'edit'>(props.modelValue.trim() === '' && props.canWrite ? 'edit' : 'read')
const text = computed({
  get: () => props.modelValue,
  set: (value: string) => emit('update:modelValue', value),
})

// ---- Transposition ----
const offset = ref(0)
const chorded = computed(() => hasChords(props.modelValue))
const result = computed(() => transposition(props.modelValue, props.musicalKey, offset.value))

function shift(semitones: number) {
  const next = offset.value + semitones
  offset.value = Math.abs(next) >= 12 ? 0 : next      // un tour complet : on revient à l'original
}

const signed = computed(() => (offset.value > 0 ? `+${offset.value}` : `${offset.value}`))
const label = computed(() => {
  const { from, to } = result.value
  if (offset.value === 0) return from ? `${from} (d’origine)` : 'Tonalité d’origine'
  return from ? `${from} → ${to} (${signed.value})` : `${signed.value} demi-tons`
})

async function onSaveTransposed() {
  if (offset.value === 0 || props.dirty) return
  const key = parseKey(props.musicalKey) ? transposeKey(props.musicalKey, offset.value) : null
  const confirmed = await confirm({
    title: `Enregistrer les accords ${result.value.to ? `en ${result.value.to}` : `à ${signed.value} demi-tons`} ?`,
    message: `Les accords des paroles sont réécrits${key ? ` et la tonalité de la version passe à ${key}` : ''}. `
      + `Pour revenir en arrière, transposez de ${offset.value > 0 ? '−' : '+'}${Math.abs(offset.value)} et enregistrez à nouveau.`,
    confirmLabel: 'Enregistrer',
  })
  if (!confirmed) return
  const lyrics = result.value.text
  offset.value = 0                                    // le texte enregistré devient la nouvelle source
  emit('save-transposed', lyrics, key)
}
</script>

<template>
  <div class="lyrics-panel">
    <div class="toolbar">
      <div v-if="canWrite" class="group" role="group" aria-label="Affichage des paroles">
        <button type="button" :class="{ secondary: mode !== 'read' }" :aria-pressed="mode === 'read'"
                @click="mode = 'read'">Lecture</button>
        <button type="button" :class="{ secondary: mode !== 'edit' }" :aria-pressed="mode === 'edit'"
                @click="mode = 'edit'">Modifier le texte</button>
      </div>
      <div v-if="chorded && mode === 'read'" class="group" role="group" aria-label="Transposition">
        <button type="button" class="secondary" aria-label="Descendre d’un demi-ton" @click="shift(-1)">−½ ton</button>
        <output class="key" aria-live="polite">{{ label }}</output>
        <button type="button" class="secondary" aria-label="Monter d’un demi-ton" @click="shift(1)">+½ ton</button>
        <button v-if="offset !== 0" type="button" class="secondary" @click="offset = 0">Revenir à l’original</button>
      </div>
    </div>

    <template v-if="mode === 'edit'">
      <textarea v-model="text" class="lyrics" rows="14" :maxlength="VERSION_LYRICS_MAX" aria-label="Paroles"
                :disabled="!canWrite" spellcheck="true" />
      <p class="muted hint">
        <small>Accords entre crochets, juste avant la syllabe : [Am]Je marche [F]seul. {c: Refrain} pour une indication.</small>
      </p>
    </template>

    <template v-else>
      <p v-if="modelValue.trim() === ''" class="muted">Pas encore de paroles.</p>
      <ChordSheet v-else :source="result.text" />
      <div v-if="canWrite && offset !== 0" class="save-transposed">
        <button type="button" class="secondary" :disabled="dirty" @click="onSaveTransposed">
          Enregistrer dans cette tonalité
        </button>
        <small v-if="dirty" class="muted">Enregistrez ou annulez d’abord vos autres modifications.</small>
      </div>
    </template>
  </div>
</template>

<style scoped>
.toolbar { display: flex; flex-wrap: wrap; gap: .75rem 1.5rem; align-items: center; margin-bottom: .75rem; }
.group { display: flex; flex-wrap: wrap; gap: .5rem; align-items: center; }
.key { min-width: 7rem; text-align: center; font-weight: 600; }
.lyrics { font-family: inherit; line-height: 1.5; }
.hint { margin: .25rem 0 0; }
.save-transposed { display: flex; flex-wrap: wrap; gap: .75rem; align-items: center; margin-top: 1rem; }
</style>