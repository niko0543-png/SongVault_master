<script setup lang="ts">
import { computed, ref } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { notificationsApi } from '../api/notificationsApi'
import { KIND_LABELS, type UnsubscribeResult } from '../types'
import { ApiError, getErrorMessage } from '@/shared/api/ApiError'
import { t } from '@/shared/i18n'

const route = useRoute()
const token = computed(() => (typeof route.query.token === 'string' ? route.query.token : ''))
// Un bouton plutôt qu'une désinscription à l'ouverture : les antivirus de messagerie ouvrent les liens tout seuls
const state = ref<'ask' | 'sending' | 'done' | 'invalid' | 'error'>(token.value ? 'ask' : 'invalid')
const result = ref<UnsubscribeResult | null>(null)
const errorMessage = ref('')

const what = computed(() =>
  result.value?.kind ? t('notifications.unsubscribe.kindEmails', { title: KIND_LABELS[result.value.kind].title }) : t('notifications.unsubscribe.noEmails'))

async function unsubscribe() {
  state.value = 'sending'
  try {
    result.value = await notificationsApi.unsubscribe(token.value)
    state.value = 'done'
  } catch (e) {
    if (e instanceof ApiError && e.status === 400) state.value = 'invalid'
    else {
      errorMessage.value = getErrorMessage(e)
      state.value = 'error'
    }
  }
}
</script>

<template>
  <section class="unsubscribe">
    <h1>{{ t('notifications.unsubscribe.title') }}</h1>
    <div v-if="state === 'ask' || state === 'sending'" class="card">
      <p>{{ t('notifications.unsubscribe.question') }}</p>
      <button type="button" :disabled="state === 'sending'" @click="unsubscribe">
        {{ state === 'sending' ? t('notifications.unsubscribe.working') : t('notifications.unsubscribe.submit') }}
      </button>
    </div>
    <div v-else-if="state === 'done'" class="card" role="status">
      <p>{{ t('notifications.unsubscribe.done', { what }) }}</p>
      <RouterLink :to="{ name: 'account' }">{{ t('notifications.unsubscribe.choose') }}</RouterLink>
    </div>
    <div v-else class="card" role="alert">
      <p v-if="state === 'invalid'">
        {{ t('notifications.unsubscribe.invalid') }}
      </p>
      <p v-else>{{ errorMessage }}</p>
      <RouterLink :to="{ name: 'account' }">{{ t('notifications.unsubscribe.goToAccount') }}</RouterLink>
    </div>
  </section>
</template>

<style scoped>
.unsubscribe { max-width: 26rem; margin: 2rem auto; }
</style>