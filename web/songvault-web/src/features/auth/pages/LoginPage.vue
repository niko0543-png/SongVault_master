<script setup lang="ts">
import { computed, nextTick, reactive, ref } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../stores/authStore'
import { authApi } from '../api/authApi'
import { isUnconfirmed, loginError, requiresTwoFactor } from '../messages'
import { safeRedirect } from '@/router/authGuard'
import { getErrorMessage } from '@/shared/api/ApiError'
import { useAsyncAction } from '@/shared/composables/useAsyncAction'
import { t } from '@/shared/i18n'

const auth = useAuthStore()
const route = useRoute()
const router = useRouter()

// Venu d'une invitation ou d'un e-mail : l'adresse est déjà remplie
const form = reactive({ email: typeof route.query.email === 'string' ? route.query.email : '', password: '', code: '' })
const { isLoading, error, run } = useAsyncAction(auth.login)

// #19 : double authentification active, le mot de passe était bon ; on demande le code (ou un code de secours)
const step = ref<'password' | 'code'>('password')
const useRecoveryCode = ref(false)
const codeInput = ref<HTMLInputElement | null>(null)

const message = computed(() => (error.value ? loginError(error.value, step.value === 'code') : null))
const canSubmit = computed(() =>
  form.email.trim() !== '' && form.password !== '' && !isLoading.value && (step.value === 'password' || form.code.trim() !== ''))

// #4 : adresse pas encore confirmée, on propose un nouvel e-mail
const unconfirmed = computed(() => isUnconfirmed(error.value))
const resent = ref(false)
const { isLoading: isResending, error: resendError, run: runResend } = useAsyncAction(authApi.resendConfirmation)

async function onSubmit() {
  if (!canSubmit.value) return
  resent.value = false
  const code = form.code.trim()
  await run({
    email: form.email.trim(),
    password: form.password,
    ...(step.value === 'code' && !useRecoveryCode.value ? { twoFactorCode: code.replace(/[\s-]/g, '') } : {}),
    ...(step.value === 'code' && useRecoveryCode.value ? { twoFactorRecoveryCode: code } : {}),
  })
  if (requiresTwoFactor(error.value)) {
    error.value = null                    // ce n'est pas une erreur : l'étape suivante
    step.value = 'code'
    await nextTick()
    codeInput.value?.focus()
    return
  }
  if (!error.value) await router.replace(safeRedirect(route.query.redirect))
}

async function toggleRecoveryCode() {
  useRecoveryCode.value = !useRecoveryCode.value
  form.code = ''
  error.value = null
  await nextTick()
  codeInput.value?.focus()
}

function backToPassword() {
  step.value = 'password'
  useRecoveryCode.value = false
  Object.assign(form, { password: '', code: '' })
  error.value = null
}

async function onResend() {
  await runResend(form.email.trim())
  resent.value = !resendError.value
}
</script>

<template>
  <section class="auth">
    <h1>{{ t('auth.login.title') }}</h1>
    <form class="card" novalidate @submit.prevent="onSubmit">
      <template v-if="step === 'password'">
        <div class="field">
          <label for="login-email">E-mail</label>
          <input id="login-email" v-model="form.email" type="email" autocomplete="username" required />
        </div>
        <div class="field">
          <label for="login-password">{{ t('auth.login.password') }}</label>
          <input id="login-password" v-model="form.password" type="password" autocomplete="current-password" required />
        </div>
      </template>
      <template v-else>
        <p class="muted">
          {{ useRecoveryCode
            ? t('auth.login.recoveryHelp')
            : t('auth.login.codeHelp') }}
        </p>
        <div class="field">
          <label for="login-code">{{ useRecoveryCode ? t('auth.login.recoveryCode') : t('auth.login.appCode') }}</label>
          <input id="login-code" ref="codeInput" v-model="form.code" type="text" required
                 :inputmode="useRecoveryCode ? 'text' : 'numeric'" :autocomplete="useRecoveryCode ? 'off' : 'one-time-code'"
                 :maxlength="useRecoveryCode ? 20 : 7" spellcheck="false" />
        </div>
      </template>
      <p v-if="message" class="field-error" role="alert">{{ message }}</p>
      <template v-if="unconfirmed">
        <p v-if="resent" class="muted" role="status">{{ t('auth.login.resent', { email: form.email.trim() }) }}</p>
        <p v-else-if="resendError" class="field-error" role="alert">{{ getErrorMessage(resendError) }}</p>
        <button v-if="!resent" type="button" class="secondary" :disabled="isResending" @click="onResend">
          {{ t('auth.login.resend') }}
        </button>
      </template>
      <button type="submit" :disabled="!canSubmit">
        {{ isLoading ? t('auth.login.signingIn') : step === 'code' ? t('auth.login.submitCode') : t('auth.login.submit') }}
      </button>
      <p v-if="step === 'code'" class="code-links">
        <button type="button" class="link" @click="toggleRecoveryCode">
          {{ useRecoveryCode ? t('auth.login.useAppCode') : t('auth.login.useRecoveryCode') }}
        </button>
        <button type="button" class="link" @click="backToPassword">{{ t('auth.login.switchAccount') }}</button>
      </p>
    </form>
    <p class="muted">
      <RouterLink :to="{ name: 'forgot-password', query: form.email.trim() ? { email: form.email.trim() } : {} }">
        {{ t('auth.login.forgot') }}
      </RouterLink>
    </p>
    <p class="muted">{{ t('auth.login.noAccount') }} <RouterLink :to="{ name: 'register', query: route.query }">{{ t('auth.login.register') }}</RouterLink></p>
  </section>
</template>

<style scoped>
.auth { max-width: 26rem; margin: 2rem auto; }
.code-links { display: flex; justify-content: space-between; gap: 1rem; margin: .75rem 0 0; }
.link { background: none; border: none; padding: 0; color: var(--primary); text-decoration: underline; cursor: pointer; }
</style>