<script setup lang="ts">
import { computed, ref } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { authApi } from '../api/authApi'
import { loginQuery, peekAfterConfirm } from '../afterConfirm'
import { getErrorMessage } from '@/shared/api/ApiError'
import { useAsyncAction } from '@/shared/composables/useAsyncAction'
import { t } from '@/shared/i18n'

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
    <h1>{{ t('auth.checkEmail.title') }}</h1>
    <div class="card">
      <p>
        {{ t('auth.checkEmail.sent') }}<template v-if="email"> {{ t('auth.checkEmail.to') }} <strong>{{ email }}</strong></template>.
        {{ t('auth.checkEmail.activate') }}
      </p>
      <p class="muted">{{ t('auth.checkEmail.help') }}</p>
      <p v-if="sent" role="status">{{ t('auth.checkEmail.resent') }}</p>
      <p v-if="error" class="field-error" role="alert">{{ getErrorMessage(error) }}</p>
      <button v-if="email" type="button" class="secondary" :disabled="isLoading" @click="onResend">
        {{ isLoading ? t('auth.checkEmail.sending') : t('auth.checkEmail.resend') }}
      </button>
    </div>
    <p class="muted">{{ t('auth.checkEmail.confirmed') }} <RouterLink :to="loginTo">{{ t('auth.checkEmail.login') }}</RouterLink></p>
  </section>
</template>

<style scoped>
.auth { max-width: 26rem; margin: 2rem auto; }
</style>