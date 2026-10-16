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
    <h1>Formules</h1>
    <p>Chaque groupe a sa formule : elle fixe la place pour ses fichiers. Les membres n'ont rien à payer.</p>

    <ErrorState v-if="error" :message="getErrorMessage(error)" @retry="load" />
    <LoadingState v-else-if="!catalog" />
    <template v-else>
      <PlanCards :plans="catalog.plans" />
      <p v-if="!catalog.onlinePayment" class="muted">Le paiement en ligne ouvrira bientôt.</p>
      <p v-if="isAuthenticated">
        Le propriétaire d'un groupe change sa formule depuis la page <strong>Facturation</strong> du groupe.
      </p>
      <p v-else><RouterLink :to="{ name: 'register' }">Créer un compte</RouterLink> : la formule Gratuit est appliquée d'office.</p>
      <p class="muted">
        <small>Prix TTC. <RouterLink :to="{ name: 'terms-of-sale' }">Conditions générales de vente</RouterLink>.</small>
      </p>
    </template>
  </section>
</template>