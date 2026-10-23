<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { storeToRefs } from 'pinia'
import { authApi } from '../api/authApi'
import { useAuthStore } from '../stores/authStore'
import { registrationErrors } from '../messages'
import SectionCard from '@/shared/components/SectionCard.vue'
import DataExportSection from '@/features/account/components/DataExportSection.vue'
import NotificationsSection from '@/features/notifications/components/NotificationsSection.vue'
import DeleteAccountSection from '@/features/account/components/DeleteAccountSection.vue'
import TwoFactorSection from '@/features/account/components/TwoFactorSection.vue'
import AccountEventsSection from '@/features/account/components/AccountEventsSection.vue'
import { useAsyncAction } from '@/shared/composables/useAsyncAction'
import { useToast } from '@/shared/composables/useToast'
import { t } from '@/shared/i18n'

const auth = useAuthStore()
const { user } = storeToRefs(auth)
const toast = useToast()

const form = reactive({ current: '', password: '', confirm: '' })
const submitted = ref(false)

// #19 : l'API renouvelle le cookie de cette session ; plus besoin de se reconnecter (ni de code)
const { isLoading, error, run } = useAsyncAction(authApi.changePassword)
const history = ref<InstanceType<typeof AccountEventsSection> | null>(null)
const mismatch = computed(() => submitted.value && form.password !== form.confirm)
const messages = computed(() => (error.value ? registrationErrors(error.value) : []))

async function onSubmit() {
  submitted.value = true
  if (!form.current || !form.password || form.password !== form.confirm) return
  await run(form.current, form.password)
  if (error.value) return
  Object.assign(form, { current: '', password: '', confirm: '' })
  submitted.value = false
  toast.success(t('auth.account.passwordChanged'))
  void history.value?.load()
}
</script>

<template>
  <section>
    <h1>{{ t('auth.account.title') }}</h1>
    <SectionCard :title="t('auth.account.email')">
      <p>{{ user?.email }}</p>
    </SectionCard>

    <SectionCard :title="t('auth.account.security')">
      <form class="password-form" novalidate @submit.prevent="onSubmit">
        <div class="field">
          <label for="account-current">{{ t('auth.account.currentPassword') }}</label>
          <input id="account-current" v-model="form.current" type="password" autocomplete="current-password" required />
        </div>
        <div class="field">
          <label for="account-password">{{ t('auth.account.newPassword') }}</label>
          <input id="account-password" v-model="form.password" type="password" autocomplete="new-password"
                 aria-describedby="account-rules" required />
          <small id="account-rules" class="muted">
            {{ t('auth.account.passwordRules') }}
          </small>
        </div>
        <div class="field">
          <label for="account-confirm">{{ t('auth.account.confirmPassword') }}</label>
          <input id="account-confirm" v-model="form.confirm" type="password" autocomplete="new-password"
                 :aria-invalid="mismatch" required />
          <p v-if="mismatch" class="field-error">{{ t('auth.account.mismatch') }}</p>
        </div>
        <ul v-if="messages.length" class="field-error" role="alert">
          <li v-for="m in messages" :key="m">{{ m }}</li>
        </ul>
        <button type="submit" :disabled="isLoading">{{ isLoading ? t('common.actions.saving') : t('auth.account.changePassword') }}</button>
      </form>
    </SectionCard>
    <TwoFactorSection @changed="history?.load()" />
    <AccountEventsSection ref="history" />
    <NotificationsSection />
    <DataExportSection />
    <DeleteAccountSection />
  </section>
</template>

<style scoped>
.password-form { max-width: 26rem; }
</style>