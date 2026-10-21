<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { authApi } from '../api/authApi'
import { loginQuery, takeAfterConfirm } from '../afterConfirm'
import { useAuthStore } from '../stores/authStore'
import LoadingState from '@/shared/components/LoadingState.vue'
import { ApiError, getErrorMessage } from '@/shared/api/ApiError'
import { t } from '@/shared/i18n'

const route = useRoute()
const auth = useAuthStore()
const state = ref<'loading' | 'done' | 'invalid' | 'error'>('loading')
const errorMessage = ref('')
const target = ref('/')

onMounted(async () => {
  const params = new URLSearchParams()
  for (const key of ['userId', 'code', 'changedEmail']) {
    const value = route.query[key]
    if (typeof value === 'string') params.set(key, value)
  }
  try {
    await authApi.confirmEmail(params)
    target.value = takeAfterConfirm()
    state.value = 'done'
  } catch (e) {
    // 401 : lien modifié, expiré (24 h) ou compte supprimé
    if (e instanceof ApiError && e.status === 401) state.value = 'invalid'
    else {
      errorMessage.value = getErrorMessage(e)
      state.value = 'error'
    }
  }
})
</script>

<template>
  <section class="auth">
    <h1>{{ t('auth.confirmEmail.title') }}</h1>
    <LoadingState v-if="state === 'loading'" :message="t('auth.confirmEmail.confirming')" />

    <div v-else-if="state === 'done'" class="card" role="status">
      <p>{{ t('auth.confirmEmail.done') }}</p>
      <RouterLink v-if="auth.isAuthenticated" :to="target">{{ t('auth.confirmEmail.continue') }}</RouterLink>
      <RouterLink v-else :to="{ name: 'login', query: loginQuery(target) }">{{ t('auth.confirmEmail.login') }}</RouterLink>
    </div>

    <div v-else class="card" role="alert">
      <p v-if="state === 'invalid'">
        {{ t('auth.confirmEmail.invalid') }}
      </p>
      <p v-else>{{ errorMessage }}</p>
      <RouterLink :to="{ name: 'login' }">{{ t('auth.confirmEmail.goToLogin') }}</RouterLink>
    </div>
  </section>
</template>

<style scoped>
.auth { max-width: 26rem; margin: 2rem auto; }
</style>