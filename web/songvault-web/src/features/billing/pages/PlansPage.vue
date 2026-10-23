<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { storeToRefs } from 'pinia'
import { billingApi } from '../api/billingApi'
import type { PlanCatalog } from '../types'
import PlanCards from '../components/PlanCards.vue'
import { useAuthStore } from '@/features/auth/stores/authStore'
import LoadingState from '@/shared/components/LoadingState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import { getErrorMessage } from '@/shared/api/ApiError'
import { t } from '@/shared/i18n'

const { isAuthenticated } = storeToRefs(useAuthStore())
const catalog = ref<PlanCatalog | null>(null)
const error = ref<unknown>(null)

async function load() {
  error.value = null
  try {
    catalog.value = await billingApi.plans()
  } catch (e) {
    error.value = e
  }
}
onMounted(load)
</script>

<template>
  <section>
    <h1>{{ t('billing.plans.title') }}</h1>
    <p>{{ t('billing.plans.intro') }}</p>

    <ErrorState v-if="error" :message="getErrorMessage(error)" @retry="load" />
    <LoadingState v-else-if="!catalog" />
    <template v-else>
      <PlanCards :plans="catalog.plans" />
      <p v-if="!catalog.onlinePayment" class="muted">{{ t('billing.plans.soon') }}</p>
      <p v-if="isAuthenticated">
        {{ t('billing.plans.ownerBefore') }} <strong>{{ t('billing.plans.billingLink') }}</strong> {{ t('billing.plans.ownerAfter') }}
      </p>
      <p v-else><RouterLink :to="{ name: 'register' }">{{ t('billing.plans.register') }}</RouterLink> {{ t('billing.plans.registerAfter') }}</p>
      <p class="muted">
        <small>{{ t('billing.plans.vat') }} <RouterLink :to="{ name: 'terms-of-sale' }">{{ t('billing.plans.termsLink') }}</RouterLink>.</small>
      </p>
    </template>
  </section>
</template>