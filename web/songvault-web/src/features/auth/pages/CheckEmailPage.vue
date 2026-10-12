<script setup lang="ts">
import { computed, ref } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { authApi } from '../api/authApi'
import { loginQuery, peekAfterConfirm } from '../afterConfirm'
import { getErrorMessage } from '@/shared/api/ApiError'
import { useAsyncAction } from '@/shared/composables/useAsyncAction'

const route = useRoute()
const email = computed(() => (typeof route.query.email === 'string' ? route.query.email : ''))
const loginTo = computed(() => ({ name: 'login', query: loginQuery(peekAfterConfirm(), email.value) }))
const sent = ref(false)
const { isLoading, error, run } = useAsyncAction(authApi.resendConfirmation)

async function onResend() {
  sent.value = false
  await run(email.value)
  sent.value = !error.value
}
</script>

<template>
  <section class="auth">
    <h1>Vérifiez votre boîte mail</h1>
    <div class="card">
      <p>
        Un e-mail de confirmation vient de partir<template v-if="email"> à <strong>{{ email }}</strong></template>.
        Cliquez sur le lien qu'il contient pour activer votre compte ; il est valable 24 heures.
      </p>
      <p class="muted">Rien reçu ? Regardez dans les indésirables, ou demandez un nouvel e-mail.</p>
      <p v-if="sent" role="status">Nouvel e-mail envoyé.</p>
      <p v-if="error" class="field-error" role="alert">{{ getErrorMessage(error) }}</p>
      <button v-if="email" type="button" class="secondary" :disabled="isLoading" @click="onResend">
        {{ isLoading ? 'Envoi…' : "Renvoyer l'e-mail" }}
      </button>
    </div>
    <p class="muted">Adresse confirmée ? <RouterLink :to="loginTo">Se connecter</RouterLink></p>
  </section>
</template>

<style scoped>
.auth { max-width: 26rem; margin: 2rem auto; }
</style>