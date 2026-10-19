<script setup lang="ts">
import { onMounted, ref } from 'vue'
import SectionCard from '@/shared/components/SectionCard.vue'
import LoadingState from '@/shared/components/LoadingState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import { getErrorMessage } from '@/shared/api/ApiError'
import { formatDate } from '@/shared/utils/format'
import { accountApi } from '../api/accountApi'
import type { AccountEvent, AccountEventType } from '../types'

const LABELS: Record<AccountEventType, string> = {
  SignedIn: 'Connexion',
  SignedInWithCode: 'Connexion avec code',
  SignedInWithRecoveryCode: 'Connexion avec un code de secours',
  SignInFailed: 'Mot de passe incorrect',
  CodeRejected: 'Code refusé',
  LockedOut: 'Compte verrouillé après trop d’essais',
  PasswordChanged: 'Mot de passe changé',
  TwoFactorEnabled: 'Double authentification activée',
  TwoFactorDisabled: 'Double authentification désactivée',
  RecoveryCodesRegenerated: 'Nouveaux codes de secours',
}
/** Ce qui mérite un coup d'œil : quelqu'un a peut-être essayé d'entrer. */
const WARNINGS: AccountEventType[] = ['SignInFailed', 'CodeRejected', 'LockedOut']

const events = ref<AccountEvent[] | null>(null)
const error = ref<unknown>(null)

async function load() {
  error.value = null
  try {
    events.value = await accountApi.accountEvents()
  } catch (e) {
    error.value = e
  }
}
onMounted(load)
defineExpose({ load })        // rechargé par la page après un changement de double authentification
</script>

<template>
  <SectionCard title="Connexions récentes">
    <p class="muted">
      Les 50 derniers événements (90 jours au plus). Une ligne que vous ne reconnaissez pas ?
      Changez votre mot de passe et activez la double authentification.
    </p>
    <ErrorState v-if="error" :message="getErrorMessage(error)" @retry="load" />
    <LoadingState v-else-if="!events" />
    <p v-else-if="events.length === 0" class="muted">Rien pour l’instant.</p>
    <ul v-else class="events" aria-label="Connexions récentes">
      <li v-for="e in events" :key="e.id" :class="{ warning: WARNINGS.includes(e.type) }" data-testid="account-event">
        <span class="what">{{ LABELS[e.type] ?? e.type }}</span>
        <span class="muted">
          <time :datetime="e.occurredAt">{{ formatDate(e.occurredAt) }}</time>
          <template v-if="e.device"> · {{ e.device }}</template>
          <template v-if="e.network"> · réseau {{ e.network }}</template>
        </span>
      </li>
    </ul>
  </SectionCard>
</template>

<style scoped>
.events { list-style: none; padding: 0; margin: 0; display: grid; gap: .35rem; }
.events li { display: flex; flex-wrap: wrap; justify-content: space-between; gap: .25rem 1rem; padding: .35rem 0; border-bottom: 1px solid var(--border); }
.events li.warning .what { color: var(--danger); font-weight: 600; }
</style>