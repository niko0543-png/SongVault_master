<script setup lang="ts">
import { computed, ref } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { notificationsApi } from '../api/notificationsApi'
import { KIND_LABELS, type UnsubscribeResult } from '../types'
import { ApiError, getErrorMessage } from '@/shared/api/ApiError'

const route = useRoute()
const token = computed(() => (typeof route.query.token === 'string' ? route.query.token : ''))
// Un bouton plutôt qu'une désinscription à l'ouverture : les antivirus de messagerie ouvrent les liens tout seuls
const state = ref<'ask' | 'sending' | 'done' | 'invalid' | 'error'>(token.value ? 'ask' : 'invalid')
const result = ref<UnsubscribeResult | null>(null)
const errorMessage = ref('')

const what = computed(() =>
  result.value?.kind ? `les e-mails « ${KIND_LABELS[result.value.kind].title} »` : 'aucun e-mail de notification')

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
    <h1>Se désinscrire</h1>
    <div v-if="state === 'ask' || state === 'sending'" class="card">
      <p>Ne plus recevoir ce type d’e-mail de SongVault ?</p>
      <button type="button" :disabled="state === 'sending'" @click="unsubscribe">
        {{ state === 'sending' ? 'Désinscription…' : 'Me désinscrire' }}
      </button>
    </div>
    <div v-else-if="state === 'done'" class="card" role="status">
      <p>C’est fait : vous ne recevrez plus {{ what }}.</p>
      <RouterLink :to="{ name: 'account' }">Choisir mes notifications</RouterLink>
    </div>
    <div v-else class="card" role="alert">
      <p v-if="state === 'invalid'">
        Ce lien de désinscription est incomplet ou a été modifié. Choisissez vos notifications dans « Mon compte ».
      </p>
      <p v-else>{{ errorMessage }}</p>
      <RouterLink :to="{ name: 'account' }">Aller à « Mon compte »</RouterLink>
    </div>
  </section>
</template>

<style scoped>
.unsubscribe { max-width: 26rem; margin: 2rem auto; }
</style>