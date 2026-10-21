<script setup lang="ts">
import type { Plan } from '../types'
import { describeBands, formatMonthlyPrice } from '../format'
import { formatFileSize } from '@/shared/utils/format'
import { t } from '@/shared/i18n'

defineProps<{
  plans: Plan[]
  /** Formule du groupe, mise en avant ; absente sur la page publique. */
  currentPlanId?: string
}>()

defineSlots<{ action?: (props: { plan: Plan }) => unknown }>()
</script>

<template>
  <ul class="plan-cards">
    <li v-for="plan in plans" :key="plan.id" class="card plan-card"
        :class="{ 'plan-card--current': plan.id === currentPlanId }" :data-testid="`plan-${plan.id}`">
      <h3>{{ plan.name }}</h3>
      <p class="plan-price">{{ formatMonthlyPrice(plan.monthlyPriceCents) }}</p>
      <ul class="plan-limits">
        <li>{{ t('billing.planCards.storage', { storageQuotaBytes: formatFileSize(plan.storageQuotaBytes) }) }}</li>
        <li>{{ describeBands(plan) }}</li>
        <li>{{ t('billing.planCards.unlimitedMembers') }}</li>
      </ul>
      <p v-if="plan.id === currentPlanId" class="muted"><strong>{{ t('billing.planCards.current') }}</strong></p>
      <slot v-else name="action" :plan="plan" />
    </li>
  </ul>
</template>

<style scoped>
.plan-cards { list-style: none; padding: 0; display: grid; gap: 1rem; grid-template-columns: repeat(auto-fit, minmax(14rem, 1fr)); }
.plan-card h3 { margin: 0; }
.plan-card--current { border-color: var(--primary); }
.plan-price { font-size: 1.2rem; font-weight: 600; margin: .25rem 0 .5rem; }
.plan-limits { padding-left: 1.1rem; margin: 0 0 .75rem; }
</style>