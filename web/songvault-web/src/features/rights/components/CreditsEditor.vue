<script setup lang="ts">
import { computed } from 'vue'
import { CREDIT_NAME_MAX, MAX_CREDITS, creditRoleLabels, type CreditRole } from '../types'
import { creditsError, formatPercent, formatShareInput, newRow, splitEvenly, totalCents, type CreditRow } from '../rules'

/** Les ayants droit d'un morceau, avec le total en direct (#22). Le parent enregistre. */
const rows = defineModel<CreditRow[]>({ required: true })
defineProps<{ disabled?: boolean }>()

const roles = Object.entries(creditRoleLabels) as [CreditRole, string][]
const total = computed(() => totalCents(rows.value))
const problem = computed(() => creditsError(rows.value))

function add() {
  // La nouvelle ligne reçoit ce qui manque pour arriver à 100 %, s'il manque quelque chose
  const missing = 10000 - total.value
  rows.value = [...rows.value, newRow('Author', missing > 0 ? formatShareInput(missing / 100) : '')]
}

function remove(key: number) {
  rows.value = rows.value.filter((r) => r.key !== key)
}

function split() {
  const shares = splitEvenly(rows.value.length)
  rows.value = rows.value.map((r, i) => ({ ...r, share: formatShareInput(shares[i]!) }))
}
</script>

<template>
  <fieldset class="credits-editor" :disabled="disabled">
    <legend>Ayants droit</legend>
    <p v-if="rows.length === 0" class="muted">Aucun crédit : ajoutez les auteurs, compositeurs, arrangeurs et éditeurs.</p>
    <div v-for="(row, index) in rows" :key="row.key" class="credit-row" data-testid="credit-row">
      <input v-model="row.name" :maxlength="CREDIT_NAME_MAX" :aria-label="`Nom de l'ayant droit ${index + 1}`"
             placeholder="Nom" autocomplete="off" />
      <select v-model="row.role" :aria-label="`Rôle de l'ayant droit ${index + 1}`">
        <option v-for="[value, label] in roles" :key="value" :value="value">{{ label }}</option>
      </select>
      <span class="share">
        <input v-model="row.share" inputmode="decimal" :aria-label="`Part de l'ayant droit ${index + 1}, en pourcentage`"
               placeholder="0" autocomplete="off" />
        <span aria-hidden="true">%</span>
      </span>
      <button type="button" class="secondary" :aria-label="`Retirer l'ayant droit ${index + 1}`" @click="remove(row.key)">
        Retirer
      </button>
    </div>
    <div class="buttons">
      <button type="button" class="secondary" :disabled="rows.length >= MAX_CREDITS" @click="add">Ajouter un ayant droit</button>
      <button v-if="rows.length >= 2" type="button" class="secondary" @click="split">Répartir à parts égales</button>
    </div>
    <p v-if="rows.length" class="total" :class="{ wrong: total !== 10000 }" role="status" data-testid="credits-total">
      Total : <strong>{{ formatPercent(total) }}</strong>
    </p>
    <p v-if="problem" class="field-error" data-testid="credits-error">{{ problem }}</p>
  </fieldset>
</template>

<style scoped>
.credits-editor { border: 0; padding: 0; margin: 0 0 1rem; }
.credits-editor legend { font-weight: 600; margin-bottom: .5rem; }
.credit-row { display: grid; grid-template-columns: minmax(0, 2fr) minmax(0, 1fr) 7rem auto; gap: .5rem; align-items: center; margin-bottom: .5rem; }
.share { display: flex; align-items: center; gap: .25rem; }
.share input { width: 100%; text-align: right; }
.buttons { display: flex; gap: .5rem; flex-wrap: wrap; }
.total { margin: .75rem 0 .25rem; }
.total.wrong strong { color: var(--danger); }
@media (max-width: 640px) { .credit-row { grid-template-columns: 1fr 1fr; } }
</style>