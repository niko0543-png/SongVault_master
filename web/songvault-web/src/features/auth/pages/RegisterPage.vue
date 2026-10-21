<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../stores/authStore'
import { registrationErrors } from '../messages'
import { rememberAfterConfirm } from '../afterConfirm'
import { useAsyncAction } from '@/shared/composables/useAsyncAction'
import { t } from '@/shared/i18n'

const auth = useAuthStore()
const route = useRoute()
const router = useRouter()

// Venu d'une invitation : l'adresse invitée est déjà remplie
const form = reactive({ email: typeof route.query.email === 'string' ? route.query.email : '', password: '', confirm: '' })
const submitted = ref(false)
const { isLoading, error, run } = useAsyncAction(auth.register)

const mismatch = computed(() => submitted.value && form.password !== form.confirm)
const messages = computed(() => (error.value ? registrationErrors(error.value) : []))

async function onSubmit() {
  submitted.value = true
  if (form.password !== form.confirm || !form.email.trim()) return
  const email = form.email.trim()
  await run({ email, password: form.password })
  if (error.value) return
  rememberAfterConfirm(route.query.redirect)          // ex. l'invitation, rouverte après la confirmation
  await router.replace({ name: 'check-email', query: { email } })
}
</script>

<template>
  <section class="auth">
    <h1>{{ t('auth.register.title') }}</h1>
    <form class="card" novalidate @submit.prevent="onSubmit">
      <div class="field">
        <label for="register-email">E-mail</label>
        <input id="register-email" v-model="form.email" type="email" autocomplete="username" required />
      </div>
      <div class="field">
        <label for="register-password">{{ t('auth.register.password') }}</label>
        <input id="register-password" v-model="form.password" type="password" autocomplete="new-password"
               aria-describedby="password-rules" required />
        <small id="password-rules" class="muted">
          {{ t('auth.register.passwordRules') }}
        </small>
      </div>
      <div class="field">
        <label for="register-confirm">{{ t('auth.register.confirmPassword') }}</label>
        <input id="register-confirm" v-model="form.confirm" type="password" autocomplete="new-password"
               :aria-invalid="mismatch" required />
        <p v-if="mismatch" class="field-error">{{ t('auth.register.mismatch') }}</p>
      </div>
      <ul v-if="messages.length" class="field-error" role="alert">
        <li v-for="m in messages" :key="m">{{ m }}</li>
      </ul>
      <button type="submit" :disabled="isLoading">{{ isLoading ? t('auth.register.creating') : t('auth.register.submit') }}</button>
    </form>
        <p class="muted">
      {{ t('auth.register.privacyBefore') }}
      <RouterLink :to="{ name: 'privacy' }">{{ t('auth.register.privacyLink') }}</RouterLink>.
    </p>
    <p class="muted">{{ t('auth.register.haveAccount') }} <RouterLink :to="{ name: 'login', query: route.query }">{{ t('auth.register.login') }}</RouterLink></p>
  </section>
</template>

<style scoped>
.auth { max-width: 26rem; margin: 2rem auto; }
</style>