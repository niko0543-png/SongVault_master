<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../stores/authStore'
import { registrationErrors } from '../messages'
import { safeRedirect } from '@/router/authGuard'
import { useAsyncAction } from '@/shared/composables/useAsyncAction'

const auth = useAuthStore()
const route = useRoute()
const router = useRouter()

const form = reactive({ email: '', password: '', confirm: '' })
const submitted = ref(false)
const { isLoading, error, run } = useAsyncAction(auth.register)

const mismatch = computed(() => submitted.value && form.password !== form.confirm)
const messages = computed(() => (error.value ? registrationErrors(error.value) : []))

async function onSubmit() {
  submitted.value = true
  if (form.password !== form.confirm || !form.email.trim()) return
  await run({ email: form.email.trim(), password: form.password })
  if (!error.value) await router.replace(safeRedirect(route.query.redirect))
}
</script>

<template>
  <section class="auth">
    <h1>Créer un compte</h1>
    <form class="card" novalidate @submit.prevent="onSubmit">
      <div class="field">
        <label for="register-email">E-mail</label>
        <input id="register-email" v-model="form.email" type="email" autocomplete="username" required />
      </div>
      <div class="field">
        <label for="register-password">Mot de passe</label>
        <input id="register-password" v-model="form.password" type="password" autocomplete="new-password"
               aria-describedby="password-rules" required />
        <small id="password-rules" class="muted">
          6 caractères minimum, avec une majuscule, une minuscule, un chiffre et un caractère spécial.
        </small>
      </div>
      <div class="field">
        <label for="register-confirm">Confirmer le mot de passe</label>
        <input id="register-confirm" v-model="form.confirm" type="password" autocomplete="new-password"
               :aria-invalid="mismatch" required />
        <p v-if="mismatch" class="field-error">Les deux mots de passe ne correspondent pas.</p>
      </div>
      <ul v-if="messages.length" class="field-error" role="alert">
        <li v-for="m in messages" :key="m">{{ m }}</li>
      </ul>
      <button type="submit" :disabled="isLoading">{{ isLoading ? 'Création…' : 'Créer mon compte' }}</button>
    </form>
    <p class="muted">Déjà un compte ? <RouterLink :to="{ name: 'login', query: route.query }">Se connecter</RouterLink></p>
  </section>
</template>

<style scoped>
.auth { max-width: 26rem; margin: 2rem auto; }
</style>