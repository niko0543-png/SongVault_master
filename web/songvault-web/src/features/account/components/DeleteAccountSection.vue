<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import SectionCard from '@/shared/components/SectionCard.vue'
import { getErrorMessage } from '@/shared/api/ApiError'
import { useConfirm } from '@/shared/composables/useConfirm'
import { useToast } from '@/shared/composables/useToast'
import { useAuthStore } from '@/features/auth/stores/authStore'
import { accountApi } from '../api/accountApi'
import { t } from '@/shared/i18n'

const router = useRouter()
const auth = useAuthStore()
const toast = useToast()
const { confirm } = useConfirm()

const password = ref('')
const error = ref<string | null>(null)
const isDeleting = ref(false)

async function onSubmit() {
  error.value = null
  if (!password.value) {
    error.value = t('account.deleteAccount.passwordRequired')
    return
  }
  const confirmed = await confirm({
    title: t('account.deleteAccount.confirmTitle'),
    message: t('account.deleteAccount.confirmMessage'),
    confirmLabel: t('account.deleteAccount.submit'),
    danger: true,
  })
  if (!confirmed) return

  isDeleting.value = true
  try {
    await accountApi.deleteAccount(password.value)
    auth.clearSession()
    toast.success(t('account.deleteAccount.done'))
    await router.replace({ name: 'login' })
  } catch (e) {
    error.value = getErrorMessage(e)
  } finally {
    isDeleting.value = false
  }
}
</script>

<template>
  <SectionCard :title="t('account.deleteAccount.submit')">
    <p class="muted">
      {{ t('account.deleteAccount.intro') }}
    </p>
    <form class="delete-form" novalidate @submit.prevent="onSubmit">
      <div class="field">
        <label for="delete-password">{{ t('account.deleteAccount.password') }}</label>
        <input id="delete-password" v-model="password" type="password" autocomplete="current-password" required />
      </div>
      <p v-if="error" class="field-error" role="alert">{{ error }}</p>
      <button type="submit" class="danger" :disabled="isDeleting">
        {{ isDeleting ? t('account.deleteAccount.deleting') : t('account.deleteAccount.submit') }}
      </button>
    </form>
  </SectionCard>
</template>

<style scoped>
.delete-form { max-width: 26rem; }
</style>