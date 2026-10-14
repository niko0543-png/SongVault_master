<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import SectionCard from '@/shared/components/SectionCard.vue'
import LoadingState from '@/shared/components/LoadingState.vue'
import { getErrorMessage } from '@/shared/api/ApiError'
import { useToast } from '@/shared/composables/useToast'
import { notificationsApi } from '../api/notificationsApi'
import { KIND_LABELS, MODE_LABELS, type NotificationKind, type NotificationMode, type NotificationPreference } from '../types'

const toast = useToast()
const kinds = Object.keys(KIND_LABELS) as NotificationKind[]
const modes = Object.keys(MODE_LABELS) as NotificationMode[]

const form = reactive<Record<NotificationKind, NotificationMode>>({ NewVersion: 'Immediate', Comment: 'Daily', Mention: 'Immediate' })
const isLoading = ref(true)
const isSaving = ref(false)
const error = ref<string | null>(null)

function fill(preferences: NotificationPreference[]) {
  for (const p of preferences) form[p.kind] = p.mode
}

onMounted(async () => {
  try {
    fill(await notificationsApi.preferences())
  } catch (e) {
    error.value = getErrorMessage(e)
  } finally {
    isLoading.value = false
  }
})

async function save() {
  if (isSaving.value) return
  isSaving.value = true
  error.value = null
  try {
    fill(await notificationsApi.update(kinds.map((kind) => ({ kind, mode: form[kind] }))))
    toast.success('Notifications enregistrées.')
  } catch (e) {
    error.value = getErrorMessage(e)
  } finally {
    isSaving.value = false
  }
}
</script>

<template>
  <SectionCard title="Notifications par e-mail">
    <p class="muted">
      Valable dans tous vos groupes. Vos propres modifications ne vous envoient jamais d’e-mail.
      Le résumé part chaque matin vers 7 h.
    </p>
    <LoadingState v-if="isLoading" message="Chargement…" />
    <form v-else class="notifications-form" novalidate @submit.prevent="save">
      <div v-for="kind in kinds" :key="kind" class="field">
        <label :for="`notif-${kind}`">{{ KIND_LABELS[kind].title }}</label>
        <select :id="`notif-${kind}`" v-model="form[kind]" :aria-describedby="`notif-${kind}-help`">
          <option v-for="mode in modes" :key="mode" :value="mode">{{ MODE_LABELS[mode] }}</option>
        </select>
        <small :id="`notif-${kind}-help`" class="muted">{{ KIND_LABELS[kind].help }}</small>
      </div>
      <p v-if="error" class="field-error" role="alert">{{ error }}</p>
      <button type="submit" :disabled="isSaving">{{ isSaving ? 'Enregistrement…' : 'Enregistrer' }}</button>
    </form>
  </SectionCard>
</template>

<style scoped>
.notifications-form { max-width: 26rem; }
</style>