<script setup lang="ts">
import { computed, reactive } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../stores/authStore'
import { loginError } from '../messages'
import { safeRedirect } from '@/router/authGuard'
import { useAsyncAction } from '@/shared/composables/useAsyncAction'

const auth = useAuthStore()
const route = useRoute()
const router = useRouter()

const form = reactive({ email: '', password: '' })
const { isLoading, error, run } = useAsyncAction(auth.login)
const message = computed(() => (error.value ? loginError(error.value) : null))
const canSubmit = computed(() => form.email.trim() !== '' && form.password !== '' && !isLoading.value)

async function onSubmit() {
  if (!canSubmit.value) return
  await run({ email: form.email.trim(), password: form.password })
  if (!error.value) await router.replace(safeRedirect(route.query.redirect))
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
      <button type="submit" :disabled="!canSubmit">{{ isLoading ? 'Connexion…' : 'Se connecter' }}</button>
    </form>
    <p class="muted">Pas encore de compte ? <RouterLink :to="{ name: 'register', query: route.query }">Créer un compte</RouterLink></p>
  </section>
</template>

<style scoped>
.auth { max-width: 26rem; margin: 2rem auto; }
</style>