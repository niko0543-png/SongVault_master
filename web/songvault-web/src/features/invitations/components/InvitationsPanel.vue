<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { useBandStore } from '@/features/bands/stores/bandStore'
import { invitationsApi } from '../api/invitationsApi'
import type { Invitation, InvitationCreated, InvitableRole } from '../types'
import SectionCard from '@/shared/components/SectionCard.vue'
import { ApiError, getErrorMessage } from '@/shared/api/ApiError'
import { formatDate } from '@/shared/utils/format'
import { useAsyncAction } from '@/shared/composables/useAsyncAction'
import { useToast } from '@/shared/composables/useToast'

const bands = useBandStore()
const toast = useToast()

const roleLabels: Record<InvitableRole, string> = { Member: 'Membre', Guest: 'Invité' }
const ROLES: InvitableRole[] = ['Member', 'Guest']

const form = reactive<{ email: string; role: InvitableRole }>({ email: '', role: 'Member' })
const pending = ref<Invitation[]>([])
const created = ref<InvitationCreated | null>(null)
const listError = ref<string | null>(null)
const busyId = ref<string | null>(null)

const { isLoading, error, run } = useAsyncAction(invitationsApi.create)
// 400 : adresse refusée par la validation ; 422 : déjà membre (le détail de l'API le dit)
const createMessage = computed(() => {
  if (!error.value) return null
  if (error.value instanceof ApiError && error.value.status === 400) return 'Adresse e-mail invalide.'
  return getErrorMessage(error.value)
})

async function load() {
  listError.value = null
  try {
    pending.value = await invitationsApi.listPending()
  } catch (e) {
    listError.value = getErrorMessage(e)
  }
}
// Changement de groupe sans quitter la page : autre liste, et le lien affiché n'est plus le bon
watch(() => bands.activeId, (id) => {
  created.value = null
  if (id) void load()
}, { immediate: true })

async function onInvite() {
  const email = form.email.trim()
  if (!email) return
  const result = await run(email, form.role)
  if (!result) return
  created.value = result
  form.email = ''
  if (result.emailSent) toast.success(`Invitation envoyée à ${result.email}.`)
  await load()
}

async function onCopy() {
  if (!created.value) return
  try {
    await navigator.clipboard.writeText(created.value.link)
    toast.success('Lien copié.')
  } catch {
    // Presse-papiers indisponible (page servie en http hors localhost) : le champ reste sélectionnable
    toast.error('Copie impossible : sélectionnez le lien et copiez-le à la main.')
  }
}

async function onRevoke(invitation: Invitation) {
  busyId.value = invitation.id
  try {
    await invitationsApi.revoke(invitation.id)
    toast.success(`Invitation de ${invitation.email} annulée.`)
  } catch (e) {
    // 410 : utilisée ou expirée entre-temps, la liste relue la fera disparaître
    if (!(e instanceof ApiError && e.status === 410)) toast.error(getErrorMessage(e))
  } finally {
    busyId.value = null
  }
  if (created.value?.id === invitation.id) created.value = null
  await load()
}
</script>

<template>
  <SectionCard title="Inviter quelqu'un">
    <form class="invite-form" novalidate @submit.prevent="onInvite">
      <div class="field">
        <label for="invite-email">E-mail</label>
        <input id="invite-email" v-model="form.email" type="email" autocomplete="off" required />
      </div>
      <div class="field">
        <label for="invite-role">Rôle</label>
        <select id="invite-role" v-model="form.role">
          <option v-for="role in ROLES" :key="role" :value="role">{{ roleLabels[role] }}</option>
        </select>
      </div>
      <button type="submit" :disabled="isLoading || !form.email.trim()">{{ isLoading ? 'Envoi…' : 'Inviter' }}</button>
    </form>
    <p v-if="createMessage" class="field-error" role="alert">{{ createMessage }}</p>

    <div v-if="created" class="created" data-testid="invitation-link">
      <p v-if="created.emailSent" class="muted">
        Lien envoyé à {{ created.email }}, valable jusqu'au {{ formatDate(created.expiresAt) }}. Il ne sera plus affiché ensuite.
      </p>
      <p v-else class="field-error" role="alert">
        L'e-mail n'a pas pu partir. Envoyez ce lien vous-même à {{ created.email }} :
      </p>
      <div class="link-row">
        <input :value="created.link" readonly aria-label="Lien d'invitation" />
        <button type="button" class="secondary" @click="onCopy">Copier le lien</button>
      </div>
    </div>

    <h3>En attente</h3>
    <p v-if="listError" class="field-error" role="alert">{{ listError }}</p>
    <p v-else-if="pending.length === 0" class="muted">Aucune invitation en attente.</p>
    <ul v-else class="invitation-list">
      <li v-for="invitation in pending" :key="invitation.id" class="invitation" data-testid="invitation-item">
        <div class="invitation-info">
          <span>{{ invitation.email }} · {{ roleLabels[invitation.role] }}</span>
          <small class="muted">Expire le {{ formatDate(invitation.expiresAt) }}</small>
        </div>
        <button type="button" class="secondary" :disabled="busyId === invitation.id" @click="onRevoke(invitation)">
          Révoquer
        </button>
      </li>
    </ul>
  </SectionCard>
</template>

<style scoped>
.invite-form { display: grid; grid-template-columns: 1fr auto auto; gap: .75rem; align-items: end; }
.invite-form select { width: auto; }
.created { margin: 1rem 0; }
.link-row { display: flex; gap: .5rem; }
.link-row input { flex: 1; min-width: 0; }
h3 { font-size: 1rem; margin: 1rem 0 .5rem; }
.invitation-list { list-style: none; padding: 0; display: grid; gap: .5rem; }
.invitation { display: grid; grid-template-columns: 1fr auto; gap: .75rem; align-items: center; }
.invitation-info { display: grid; overflow-wrap: anywhere; }
@media (max-width: 600px) { .invite-form { grid-template-columns: 1fr; } }
</style>