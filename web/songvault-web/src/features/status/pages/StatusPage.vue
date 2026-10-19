<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { statusApi } from '../api/statusApi'
import { STATE_LABELS, type ServiceState, type StatusReport } from '../types'
import { formatDate } from '@/shared/utils/format'

/** Rafraîchie seule, comme une page de statut qu'on laisse ouverte pendant un incident. */
const REFRESH_MS = 60_000

const report = ref<StatusReport | null>(null)
const loading = ref(true)
const unreachable = ref(false)
const lastTry = ref<Date | null>(null)
let timer: ReturnType<typeof setInterval> | undefined

const HEADLINES: Record<ServiceState, string> = {
  ok: 'Tout fonctionne',
  degraded: 'SongVault fonctionne, avec du retard',
  down: 'SongVault est en panne',
}

const headline = computed(() => {
  if (unreachable.value) return 'SongVault ne répond pas'
  return report.value ? HEADLINES[report.value.status] : ''
})

async function refresh() {
  const next = await statusApi.get()
  unreachable.value = next === null
  if (next) report.value = next
  lastTry.value = new Date()
  loading.value = false
}

onMounted(() => {
  void refresh()
  timer = setInterval(() => void refresh(), REFRESH_MS)
})
onUnmounted(() => clearInterval(timer))
</script>

<template>
  <section class="status-page">
    <h1>État du service</h1>
    <p v-if="loading" class="muted" role="status">Vérification…</p>
    <template v-else>
      <p class="headline" :class="unreachable ? 'down' : report?.status" role="status" aria-live="polite">
        {{ headline }}
      </p>
      <p v-if="unreachable" class="muted">
        Le site s’affiche, mais le serveur ne répond pas. Nous sommes prévenus ; cette page se met à jour toute seule.
      </p>
      <ul v-else-if="report" class="components card">
        <li v-for="component in report.components" :key="component.name">
          <span>{{ component.name }}</span>
          <span class="state" :class="component.status">{{ STATE_LABELS[component.status] }}</span>
        </li>
      </ul>
      <p v-if="report && !unreachable" class="muted">Vérifié le {{ formatDate(report.checkedAt) }}.</p>
      <p v-else-if="lastTry" class="muted">Dernier essai le {{ formatDate(lastTry.toISOString()) }}.</p>
      <button type="button" class="secondary" @click="refresh">Vérifier maintenant</button>
    </template>
  </section>
</template>

<style scoped>
.status-page { max-width: 32rem; margin: 2rem auto; }
.headline { font-size: 1.25rem; font-weight: 600; }
.components { list-style: none; padding: 0; }
.components li { display: flex; justify-content: space-between; gap: 1rem; padding: 0.75rem 1rem; }
.components li + li { border-top: 1px solid var(--border); }
.ok { color: #1b7f3a; }
.degraded { color: #a35c00; }
.down { color: var(--danger); }
</style>