<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { invitationsApi } from '../api/invitationsApi'
import type { InvitableRole, InvitationPreview } from '../types'
import { useAuthStore } from '@/features/auth/stores/authStore'
import { useBandStore } from '@/features/bands/stores/bandStore'
import LoadingState from '@/shared/components/LoadingState.vue'
import { ApiError, getErrorMessage } from '@/shared/api/ApiError'
import { formatDate } from '@/shared/utils/format'
import { useAsyncAction } from '@/shared/composables/useAsyncAction'

const props = defineProps<{ token: string }>()

const auth = useAuthStore()
const bands = useBandStore()
const route = useRoute()
const router = useRouter()

const roleLabels: Record<InvitableRole, string> = {
  Member: 'membre (créer et modifier les morceaux)',
  Guest: 'invité (lire et écouter)',
}

const preview = ref<InvitationPreview | null>(null)
const loadError = ref<unknown>(null)
const isLoading = ref(true)
const { isLoading: isJoining, error: joinError, run } = useAsyncAction(invitationsApi.accept)

/** Query des pages d'inscription et de connexion : adresse préremplie, puis retour ici pour rejoindre. */
const back = computed(() => ({ email: preview.value?.email, redirect: `/invite/${props.token}?accept=1` }))
const otherAccount = computed(() =>
  auth.user !== null && preview.value !== null
  && auth.user.email.toLowerCase() !== preview.value.email.toLowerCase())

function messageFor(e: unknown): string {
  if (e instanceof ApiError && e.status === 404)
    return "Ce lien d'invitation n'existe pas. Vérifiez qu'il a été copié en entier."
  if (e instanceof ApiError && e.status === 410)
    return "Cette invitation n'est plus valable (déjà utilisée, annulée ou expirée). Demandez un nouveau lien à la personne qui vous a invité."
  return getErrorMessage(e)
}

async function join() {
  const band = await run(props.token)
  if (!band) return
  bands.reset()                         // la liste des groupes a changé : la garde la relira
  await router.replace({ name: 'songs', params: { bandId: band.id } })
}

async function switchAccount() {
  await auth.logout()
  await router.replace({ name: 'login', query: back.value })
}

onMounted(async () => {
  try {
    preview.value = await invitationsApi.preview(props.token)
  } catch (e) {
    loadError.value = e
  } finally {
    isLoading.value = false
  }
  if (preview.value && auth.isAuthenticated && !otherAccount.value && route.query.accept === '1') await join()
})
</script>

<template>
  <section class="invite">
    <h1>Invitation</h1>
    <LoadingState v-if="isLoading" />

    <div v-else-if="loadError" class="card" role="alert">
      <p>{{ messageFor(loadError) }}</p>
      <RouterLink to="/">Retour à l'accueil</RouterLink>
    </div>

    <div v-else-if="preview" class="card">
      <p>Vous êtes invité à rejoindre <strong>{{ preview.bandName }}</strong> en tant que {{ roleLabels[preview.role] }}.</p>
      <p class="muted">Invitation pour {{ preview.email }}, valable jusqu'au {{ formatDate(preview.expiresAt) }}.</p>

      <template v-if="!auth.isAuthenticated">
        <p>Créez un compte avec cette adresse, ou connectez-vous si vous en avez déjà un.</p>
        <div class="actions">
          <RouterLink :to="{ name: 'register', query: back }">Créer un compte</RouterLink>
          <RouterLink :to="{ name: 'login', query: back }">J'ai déjà un compte</RouterLink>
        </div>
      </template>

      <template v-else-if="otherAccount">
        <p class="field-error" role="alert">
          Vous êtes connecté avec {{ auth.user?.email }}. Cette invitation est destinée à {{ preview.email }} :
          connectez-vous avec ce compte pour la valider.
        </p>
        <button type="button" class="secondary" @click="switchAccount">Changer de compte</button>
      </template>

      <template v-else>
        <p v-if="joinError" class="field-error" role="alert">{{ messageFor(joinError) }}</p>
        <button type="button" :disabled="isJoining" @click="join">
          {{ isJoining ? 'Adhésion…' : 'Rejoindre le groupe' }}
        </button>
      </template>
    </div>
  </section>
</template>

<style scoped>
.invite { max-width: 32rem; margin: 2rem auto; }
.actions { display: flex; gap: 1.5rem; flex-wrap: wrap; }
</style>