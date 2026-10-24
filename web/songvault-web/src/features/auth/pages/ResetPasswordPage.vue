<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { authApi } from '../api/authApi'
import { registrationErrors } from '../messages'
import { ApiError } from '@/shared/api/ApiError'
import { useAsyncAction } from '@/shared/composables/useAsyncAction'
import { useToast } from '@/shared/composables/useToast'
import { t } from '@/shared/i18n'

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

// Le lien est vérifié dès son ouverture : déjà utilisé ou expiré, le formulaire ne s'affiche pas
const check = useAsyncAction(authApi.checkResetCode)
const linkChecked = ref(false)
const linkInvalid = computed(() => check.error.value instanceof ApiError && 'InvalidToken' in check.error.value.validationErrors)
onMounted(async () => {
  if (!email || !code) return
  await check.run(email, code)
  linkChecked.value = true          // autre erreur (réseau…) : le formulaire s'affiche, l'envoi tranchera
})

async function onSubmit() {
  submitted.value = true
  if (!form.password || form.password !== form.confirm) return
  await run(email, code, form.password)
  if (error.value) return
  toast.success(t('auth.resetPassword.done'))
  await router.replace({ name: 'login', query: { email } })
}
</script>

<template>
  <section class="auth">
    <h1>{{ t('auth.resetPassword.title') }}</h1>
    <div v-if="!email || !code" class="card" role="alert">
      <p>{{ t('auth.resetPassword.incomplete') }}</p>
      <RouterLink :to="{ name: 'forgot-password' }">{{ t('auth.resetPassword.askNew') }}</RouterLink>
    </div>
    <p v-else-if="!linkChecked" class="muted" role="status">{{ t('auth.resetPassword.checking') }}</p>
    <div v-else-if="linkInvalid" class="card" role="alert">
      <p>{{ t('auth.resetPassword.linkInvalid') }}</p>
      <RouterLink :to="{ name: 'forgot-password', query: { email } }">{{ t('auth.resetPassword.askNew') }}</RouterLink>
    </div>
    <form v-else class="card" novalidate @submit.prevent="onSubmit">
      <p class="muted">{{ t('auth.resetPassword.account', { email }) }}</p>
      <div class="field">
        <label for="reset-password">{{ t('auth.resetPassword.title') }}</label>
        <input id="reset-password" v-model="form.password" type="password" autocomplete="new-password"
               aria-describedby="password-rules" required />
        <small id="password-rules" class="muted">
          {{ t('auth.resetPassword.passwordRules') }}
        </small>
      </div>
      <div class="field">
        <label for="reset-confirm">{{ t('auth.resetPassword.confirmPassword') }}</label>
        <input id="reset-confirm" v-model="form.confirm" type="password" autocomplete="new-password"
               :aria-invalid="mismatch" required />
        <p v-if="mismatch" class="field-error">{{ t('auth.resetPassword.mismatch') }}</p>
      </div>
      <ul v-if="messages.length" class="field-error" role="alert">
        <li v-for="m in messages" :key="m">{{ m }}</li>
      </ul>
      <p v-if="invalidToken"><RouterLink :to="{ name: 'forgot-password', query: { email } }">{{ t('auth.resetPassword.askNew') }}</RouterLink></p>
      <button type="submit" :disabled="isLoading">{{ isLoading ? t('common.actions.saving') : t('auth.resetPassword.submit') }}</button>
    </form>
  </section>
</template>

<style scoped>
.auth { max-width: 26rem; margin: 2rem auto; }
</style>