
<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { billingApi } from '../api/billingApi'
import { redirectTo } from '../redirect'
import { statusLabels } from '../format'
import type { Billing, Plan } from '../types'
import StorageGauge from '../components/StorageGauge.vue'
import PlanCards from '../components/PlanCards.vue'
import { useBandStore } from '@/features/bands/stores/bandStore'
import { publisher } from '@/features/legal/publisher'
import LoadingState from '@/shared/components/LoadingState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import { ApiError, getErrorMessage } from '@/shared/api/ApiError'
import { formatDate } from '@/shared/utils/format'

const bands = useBandStore()
const route = useRoute()

const billing = ref<Billing | null>(null)
const plans = ref<Plan[]>([])
const isLoading = ref(true)
const error = ref<unknown>(null)
const actionError = ref<string | null>(null)
const busy = ref<string | null>(null)

/** Retour de Stripe Checkout : la formule change à la réception du webhook, quelques secondes plus tard. */
const isBackFromCheckout = computed(() => route.query.checkout === 'success')
const isWaitingForStripe = computed(() => isBackFromCheckout.value && billing.value?.usage.planId === 'free')
const RECHECKS = 5
let rechecks = 0
let timer: ReturnType<typeof setTimeout> | undefined

async function load() {
  error.value = null
  try {
    const [b, catalog] = await Promise.all([billingApi.billing(), billingApi.plans()])
    billing.value = b
    plans.value = catalog.plans
  } catch (e) {
    error.value = e
  } finally {
    isLoading.value = false
  }
  if (isWaitingForStripe.value && rechecks < RECHECKS) {
    rechecks++
    timer = setTimeout(() => void load(), 2000)
  }
}
watch(() => bands.activeId, (id) => { if (id) { isLoading.value = true; rechecks = 0; void load() } }, { immediate: true })
onUnmounted(() => clearTimeout(timer))

const period = computed(() => {
  const b = billing.value
  if (!b?.currentPeriodEnd || b.isGranted || b.usage.planId === 'free') return null
  return b.cancelAtPeriodEnd
    ? `Résilié : la formule ${b.usage.planName} s'arrête le ${formatDate(b.currentPeriodEnd)}, rien ne sera effacé.`
    : `Prochain paiement le ${formatDate(b.currentPeriodEnd)}.`
})

/** Le message de l'API pour un 503 (« pas encore ouvert », « Stripe injoignable »), plutôt que le générique. */
function message(e: unknown): string {
  return e instanceof ApiError && e.status === 503 && e.problem.detail ? e.problem.detail : getErrorMessage(e)
}

async function go(key: string, action: () => Promise<{ url: string }>) {
  busy.value = key
  actionError.value = null
  try {
    redirectTo((await action()).url)
  } catch (e) {
    actionError.value = message(e)
    busy.value = null
  }
}
const openPortal = () => go('portal', billingApi.portal)
const checkout = (planId: string) => go(planId, () => billingApi.checkout(planId))
</script>

<template>
  <section>
    <header class="page-header">
      <h1>Facturation</h1>
    </header>

    <LoadingState v-if="isLoading" />
    <ErrorState v-else-if="error" :message="getErrorMessage(error)" @retry="load" />

    <template v-else-if="billing">
      <p v-if="isWaitingForStripe" role="status" class="card">
        Paiement reçu. La formule s'applique dès que Stripe le confirme : cette page se met à jour toute seule.
      </p>
      <p v-else-if="isBackFromCheckout" role="status" class="card">
        Merci ! Le groupe est maintenant sur la formule {{ billing.usage.planName }}.
      </p>

      <StorageGauge :usage="billing.usage" />

      <p v-if="billing.isGranted">Formule {{ billing.usage.planName }} offerte : rien à payer.</p>
      <p v-else-if="billing.status">
        Abonnement : <strong>{{ statusLabels[billing.status] }}</strong>.
        <template v-if="billing.status === 'PastDue'">
          Le dernier paiement a été refusé : mettez à jour la carte, la formule reste en place pendant les nouvelles tentatives.
        </template>
      </p>
      <p v-if="period">{{ period }}</p>

      <p v-if="billing.canOpenPortal">
        <button type="button" :disabled="busy !== null" @click="openPortal">
          Gérer l'abonnement
        </button>
        <span class="muted"> Changer de formule ou de carte, voir les factures, résilier.</span>
      </p>

      <p v-if="!billing.onlinePayment" class="card" data-testid="payment-closed">
        Le paiement en ligne n'est pas encore ouvert. Pour passer à une formule supérieure, écrivez à
        <a :href="`mailto:${publisher.email}`">{{ publisher.email }}</a>.
      </p>

      <p v-if="actionError" class="field-error" role="alert">{{ actionError }}</p>

      <h2>Formules</h2>
      <PlanCards :plans="plans" :current-plan-id="billing.usage.planId">
        <template #action="{ plan }">
          <button v-if="billing.canCheckout && plan.monthlyPriceCents > 0" type="button" :disabled="busy !== null"
                 @click="checkout(plan.id)">
            {{ busy === plan.id ? 'Ouverture du paiement…' : `Passer à ${plan.name}` }}
          </button>
        </template>
      </PlanCards>
      <p v-if="billing.canCheckout" class="muted">
        <small>
          Paiement par carte sur une page sécurisée de Stripe ; SongVault ne voit jamais votre numéro de carte.
          En choisissant une formule, vous acceptez les
          <RouterLink :to="{ name: 'terms-of-sale' }">conditions générales de vente</RouterLink>.
          Sans engagement : résiliable à tout moment, effet à la fin du mois payé.
        </small>
      </p>
    </template>
  </section>
</template>