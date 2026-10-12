<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { authApi } from '../api/authApi'
import { registrationErrors } from '../messages'
import { ApiError } from '@/shared/api/ApiError'
import { useAsyncAction } from '@/shared/composables/useAsyncAction'
import { useToast } from '@/shared/composables/useToast'

const route = useRoute()
const router = useRouter()
const toast = useToast()
const email = typeof route.query.email === 'string' ? route.query.email : ''
const code = typeof route.query.code === 'string' ? route.query.code : ''

const form = reactive({ password: '', confirm: '' })
const submitted = ref(false)
const { isLoading, error, run } = useAsyncAction(authApi.resetPassword)
const mismatch = computed(() => submitted.value && form.password !== form.confirm)
const messages = computed(() => (error.value ? registrationErrors(error.value) : []))
// Code expiré (2 h), déjà utilisé ou modifié
const invalidToken = computed(() => error.value instanceof ApiError && 'InvalidToken' in error.value.validationErrors)

async function onSubmit() {
  submitted.value = true
  if (!form.password || form.password !== form.confirm) return
  await run(email, code, form.password)
  if (error.value) return
  toast.success('Mot de passe modifié. Connectez-vous avec le nouveau.')
  await router.replace({ name: 'login', query: { email } })
}
</script>

<template>
  <section class="auth">
    <h1>Nouveau mot de passe</h1>
    <div v-if="!email || !code" class="card" role="alert">
      <p>Ce lien est incomplet. Copiez-le en entier depuis l'e-mail, ou demandez-en un nouveau.</p>
      <RouterLink :to="{ name: 'forgot-password' }">Demander un nouveau lien</RouterLink>
    </div>
    <form v-else class="card" novalidate @submit.prevent="onSubmit">
      <p class="muted">Compte : {{ email }}</p>
      <div class="field">
        <label for="reset-password">Nouveau mot de passe</label>
        <input id="reset-password" v-model="form.password" type="password" autocomplete="new-password"
               aria-describedby="password-rules" required />
        <small id="password-rules" class="muted">
          6 caractères minimum, avec une majuscule, une minuscule, un chiffre et un caractère spécial.
        </small>
      </div>
      <div class="field">
        <label for="reset-confirm">Confirmer le mot de passe</label>
        <input id="reset-confirm" v-model="form.confirm" type="password" autocomplete="new-password"
               :aria-invalid="mismatch" required />
        <p v-if="mismatch" class="field-error">Les deux mots de passe ne correspondent pas.</p>
      </div>
      <ul v-if="messages.length" class="field-error" role="alert">
        <li v-for="m in messages" :key="m">{{ m }}</li>
      </ul>
      <p v-if="invalidToken"><RouterLink :to="{ name: 'forgot-password', query: { email } }">Demander un nouveau lien</RouterLink></p>
      <button type="submit" :disabled="isLoading">{{ isLoading ? 'Enregistrement…' : 'Changer le mot de passe' }}</button>
    </form>
  </section>
</template>

<style scoped>
.auth { max-width: 26rem; margin: 2rem auto; }
</style>