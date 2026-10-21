<script setup lang="ts">
import { reactive } from 'vue'
import { SETLIST_NAME_MAX, SETLIST_VENUE_MAX, type SetlistInput } from '../types'
import { t } from '@/shared/i18n'

/** Nom, date et lieu d'une setlist : création (liste) et modification (détail). */
const props = defineProps<{
  initial?: SetlistInput
  submitLabel: string
  submitting?: boolean
  error?: string | null
}>()
const emit = defineEmits<{ submit: [input: SetlistInput]; cancel: [] }>()

const form = reactive({
  name: props.initial?.name ?? '',
  date: props.initial?.date ?? '',
  venue: props.initial?.venue ?? '',
})

function onSubmit() {
  const name = form.name.trim()
  if (!name) return
  emit('submit', { name, date: form.date || null, venue: form.venue.trim() || null })
}
</script>

<template>
  <form class="setlist-form" @submit.prevent="onSubmit">
    <div class="field">
      <label for="setlist-name">{{ t('setlists.form.name') }}</label>
      <input id="setlist-name" v-model="form.name" required :maxlength="SETLIST_NAME_MAX" autocomplete="off" />
    </div>
    <div class="field">
      <label for="setlist-date">{{ t('setlists.form.date') }} <small class="muted">{{ t('setlists.form.optionalDate') }}</small></label>
      <input id="setlist-date" v-model="form.date" type="date" />
    </div>
    <div class="field">
      <label for="setlist-venue">{{ t('setlists.form.venue') }} <small class="muted">{{ t('setlists.form.optionalVenue') }}</small></label>
      <input id="setlist-venue" v-model="form.venue" :maxlength="SETLIST_VENUE_MAX" autocomplete="off" />
    </div>
    <p v-if="error" class="field-error" role="alert">{{ error }}</p>
    <div class="buttons">
      <button type="submit" :disabled="submitting">{{ submitLabel }}</button>
      <button v-if="initial" type="button" class="secondary" @click="emit('cancel')">{{ t('common.actions.cancel') }}</button>
    </div>
  </form>
</template>

<style scoped>
.setlist-form { display: grid; grid-template-columns: 2fr 1fr 1.5fr; gap: 0 .75rem; align-items: end; }
.setlist-form .field-error, .setlist-form .buttons { grid-column: 1 / -1; }
.buttons { display: flex; gap: .5rem; }
@media (max-width: 640px) { .setlist-form { grid-template-columns: 1fr; } }
</style>