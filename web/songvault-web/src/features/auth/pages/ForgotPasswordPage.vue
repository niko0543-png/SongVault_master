<script setup lang="ts">
import { reactive, ref } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { authApi } from '../api/authApi'
import { getErrorMessage } from '@/shared/api/ApiError'
import { useAsyncAction } from '@/shared/composables/useAsyncAction'

const route = useRoute()
const form = reactive({ email: typeof route.query.email === 'string' ? route.query.email : '' })
const sentTo = ref<string | null>(null)
const { isLoading, error, run } = useAsyncAction(authApi.forgotPassword)

async function onSubmit() {
  const email = form.email.trim()
  if (!email) return
  await run(email)
  if (!error.value) sentTo.value = email
}
</script>

<template>
  <section class="auth">
    <h1>Mot de passe oublié</h1>
    <!-- Même message que le compte existe ou non : la page ne dit pas qui est inscrit -->
    <div v-if="sentTo" class="card" role="status">
      <p>
        Si un compte confirmé existe pour <strong>{{ sentTo }}</strong>, un e-mail vient de partir avec un lien
        pour choisir un nouveau mot de passe. Le lien est valable 2 heures.
      </p>
      <RouterLink :to="{ name: 'login' }">Retour à la connexion</RouterLink>
    </div>
    <form v-else class="card" novalidate @submit.prevent="onSubmit">
      <div class="field">
        <label for="forgot-email">E-mail</label>
        <input id="forgot-email" v-model="form.email" type="email" autocomplete="username" required />
      </div>
      <p v-if="error" class="field-error" role="alert">{{ getErrorMessage(error) }}</p>
      <button type="submit" :disabled="isLoading || !form.email.trim()">{{ isLoading ? 'Envoi…' : 'Recevoir un lien' }}</button>
    </form>
  </section>
</template>

<style scoped>
.auth { max-width: 26rem; margin: 2rem auto; }
</style>