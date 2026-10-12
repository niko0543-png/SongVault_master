<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../stores/authStore'
import { authApi } from '../api/authApi'
import { isUnconfirmed, loginError } from '../messages'
import { safeRedirect } from '@/router/authGuard'
import { getErrorMessage } from '@/shared/api/ApiError'
import { useAsyncAction } from '@/shared/composables/useAsyncAction'

const auth = useAuthStore()
const route = useRoute()
const router = useRouter()

// Venu d'une invitation ou d'un e-mail : l'adresse est déjà remplie
const form = reactive({ email: typeof route.query.email === 'string' ? route.query.email : '', password: '' })
const { isLoading, error, run } = useAsyncAction(auth.login)
const message = computed(() => (error.value ? loginError(error.value) : null))
const canSubmit = computed(() => form.email.trim() !== '' && form.password !== '' && !isLoading.value)

// #4 : adresse pas encore confirmée, on propose un nouvel e-mail
const unconfirmed = computed(() => isUnconfirmed(error.value))
const resent = ref(false)
const { isLoading: isResending, error: resendError, run: runResend } = useAsyncAction(authApi.resendConfirmation)

async function onSubmit() {
  if (!canSubmit.value) return
  resent.value = false
  await run({ email: form.email.trim(), password: form.password })
  if (!error.value) await router.replace(safeRedirect(route.query.redirect))
}

async function onResend() {
  await runResend(form.email.trim())
  resent.value = !resendError.value
}
</script>

<template>
  <section class="auth">
    <h1>Connexion</h1>
    <form class="card" novalidate @submit.prevent="onSubmit">
      <div class="field">
        <label for="login-email">E-mail</label>
        <input id="login-email" v-model="form.email" type="email" autocomplete="username" required />
      </div>
      <div class="field">
        <label for="login-password">Mot de passe</label>
        <input id="login-password" v-model="form.password" type="password" autocomplete="current-password" required />
      </div>
      <p v-if="message" class="field-error" role="alert">{{ message }}</p>
      <template v-if="unconfirmed">
        <p v-if="resent" class="muted" role="status">Nouvel e-mail envoyé à {{ form.email.trim() }}.</p>
        <p v-else-if="resendError" class="field-error" role="alert">{{ getErrorMessage(resendError) }}</p>
        <button v-if="!resent" type="button" class="secondary" :disabled="isResending" @click="onResend">
          Renvoyer l'e-mail de confirmation
        </button>
      </template>
      <button type="submit" :disabled="!canSubmit">{{ isLoading ? 'Connexion…' : 'Se connecter' }}</button>
    </form>
    <p class="muted">
      <RouterLink :to="{ name: 'forgot-password', query: form.email.trim() ? { email: form.email.trim() } : {} }">
        Mot de passe oublié ?
      </RouterLink>
    </p>
    <p class="muted">Pas encore de compte ? <RouterLink :to="{ name: 'register', query: route.query }">Créer un compte</RouterLink></p>
  </section>
</template>

<style scoped>
.auth { max-width: 26rem; margin: 2rem auto; }
</style>