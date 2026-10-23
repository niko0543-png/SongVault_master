<script setup lang="ts">
import { computed, ref } from 'vue'
import ChordSheet from './ChordSheet.vue'
import { hasChords, transposition } from '../chordpro'
import { parseKey, transposeKey } from '../chords'
import { VERSION_LYRICS_MAX } from '@/features/versions/types'
import { useConfirm } from '@/shared/composables/useConfirm'
import { t } from '@/shared/i18n'

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
  if (offset.value === 0) return from ? t('chords.lyrics.original', { key: from }) : t('chords.lyrics.originalKey')
  return from ? `${from} → ${to} (${signed.value})` : t('chords.lyrics.semitones', { n: signed.value })
})

async function onSaveTransposed() {
  if (offset.value === 0 || props.dirty) return
  const key = parseKey(props.musicalKey) ? transposeKey(props.musicalKey, offset.value) : null
  const confirmed = await confirm({
    title: result.value.to
      ? t('chords.lyrics.saveInKeyTitle', { key: result.value.to })
      : t('chords.lyrics.saveBySemitonesTitle', { n: signed.value }),
    message: `${key ? t('chords.lyrics.rewrittenWithKey', { key }) : t('chords.lyrics.rewritten')} `
      + t('chords.lyrics.undo', { back: `${offset.value > 0 ? '−' : '+'}${Math.abs(offset.value)}` }),
    confirmLabel: t('common.actions.save'),
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
      <div v-if="canWrite" class="group" role="group" :aria-label="t('chords.lyrics.viewLabel')">
        <button type="button" :class="{ secondary: mode !== 'read' }" :aria-pressed="mode === 'read'"
                @click="mode = 'read'">{{ t('chords.lyrics.read') }}</button>
        <button type="button" :class="{ secondary: mode !== 'edit' }" :aria-pressed="mode === 'edit'"
                @click="mode = 'edit'">{{ t('chords.lyrics.edit') }}</button>
      </div>
      <div v-if="chorded && mode === 'read'" class="group" role="group" :aria-label="t('chords.lyrics.transposition')">
        <button type="button" class="secondary" :aria-label="t('chords.lyrics.down')" @click="shift(-1)">{{ t('chords.lyrics.downShort') }}</button>
        <output class="key" aria-live="polite">{{ label }}</output>
        <button type="button" class="secondary" :aria-label="t('chords.lyrics.up')" @click="shift(1)">{{ t('chords.lyrics.upShort') }}</button>
        <button v-if="offset !== 0" type="button" class="secondary" @click="offset = 0">{{ t('chords.lyrics.reset') }}</button>
      </div>
    </div>

    <template v-if="mode === 'edit'">
      <textarea v-model="text" class="lyrics" rows="14" :maxlength="VERSION_LYRICS_MAX" :aria-label="t('chords.lyrics.lyrics')"
                :disabled="!canWrite" spellcheck="true" />
      <p class="muted hint">
        <small>{{ t('chords.lyrics.help') }}</small>
      </p>
    </template>

    <template v-else>
      <p v-if="modelValue.trim() === ''" class="muted">{{ t('chords.lyrics.empty') }}</p>
      <ChordSheet v-else :source="result.text" />
      <div v-if="canWrite && offset !== 0" class="save-transposed">
        <button type="button" class="secondary" :disabled="dirty" @click="onSaveTransposed">
          {{ t('chords.lyrics.saveInKey') }}
        </button>
        <small v-if="dirty" class="muted">{{ t('chords.lyrics.saveBlocked') }}</small>
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