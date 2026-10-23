<script setup lang="ts">
import { onMounted, ref } from 'vue'
import SectionCard from '@/shared/components/SectionCard.vue'
import LoadingState from '@/shared/components/LoadingState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import { getErrorMessage } from '@/shared/api/ApiError'
import { formatDate } from '@/shared/utils/format'
import { accountApi } from '../api/accountApi'
import type { AccountEvent, AccountEventType } from '../types'
import { t } from '@/shared/i18n'

const LABELS: Record<AccountEventType, string> = {
  SignedIn: t('account.events.kinds.login'),
  SignedInWithCode: t('account.events.kinds.loginWithCode'),
  SignedInWithRecoveryCode: t('account.events.kinds.loginWithRecovery'),
  SignInFailed: t('account.events.kinds.wrongPassword'),
  CodeRejected: t('account.events.kinds.codeRejected'),
  LockedOut: t('account.events.kinds.lockedOut'),
  PasswordChanged: t('account.events.kinds.passwordChanged'),
  TwoFactorEnabled: t('account.events.kinds.twoFactorEnabled'),
  TwoFactorDisabled: t('account.events.kinds.twoFactorDisabled'),
  RecoveryCodesRegenerated: t('account.events.kinds.recoveryCodesReset'),
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
  <SectionCard :title="t('account.events.title')">
    <p class="muted">
      {{ t('account.events.intro') }}
    </p>
    <ErrorState v-if="error" :message="getErrorMessage(error)" @retry="load" />
    <LoadingState v-else-if="!events" />
    <p v-else-if="events.length === 0" class="muted">{{ t('account.events.empty') }}</p>
    <ul v-else class="events" :aria-label="t('account.events.title')">
      <li v-for="e in events" :key="e.id" :class="{ warning: WARNINGS.includes(e.type) }" data-testid="account-event">
        <span class="what">{{ LABELS[e.type] ?? e.type }}</span>
        <span class="muted">
          <time :datetime="e.occurredAt">{{ formatDate(e.occurredAt) }}</time>
          <template v-if="e.device"> · {{ e.device }}</template>
          <template v-if="e.network"> {{ t('account.events.network', { network: e.network }) }}</template>
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