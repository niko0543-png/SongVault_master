<script setup lang="ts">
import { reactive, ref } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { authApi } from '../api/authApi'
import { getErrorMessage } from '@/shared/api/ApiError'
import { useAsyncAction } from '@/shared/composables/useAsyncAction'
import { t } from '@/shared/i18n'

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
    <h1>{{ t('auth.forgotPassword.title') }}</h1>
    <!-- Même message que le compte existe ou non : la page ne dit pas qui est inscrit -->
    <div v-if="sentTo" class="card" role="status">
      <p>
        {{ t('auth.forgotPassword.sentBefore') }} <strong>{{ sentTo }}</strong>{{ t('auth.forgotPassword.sentAfter') }}
      </p>
      <RouterLink :to="{ name: 'login' }">{{ t('auth.forgotPassword.back') }}</RouterLink>
    </div>
    <form v-else class="card" novalidate @submit.prevent="onSubmit">
      <div class="field">
        <label for="forgot-email">E-mail</label>
        <input id="forgot-email" v-model="form.email" type="email" autocomplete="username" required />
      </div>
      <p v-if="error" class="field-error" role="alert">{{ getErrorMessage(error) }}</p>
      <button type="submit" :disabled="isLoading || !form.email.trim()">{{ isLoading ? t('auth.forgotPassword.sending') : t('auth.forgotPassword.submit') }}</button>
    </form>
  </section>
</template>

<style scoped>
.auth { max-width: 26rem; margin: 2rem auto; }
</style>