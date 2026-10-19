<script setup lang="ts">
import { ref, watch } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { setlistsApi } from '../api/setlistsApi'
import type { SetlistInput, SetlistSummary } from '../types'
import { formatSetlistDate } from '../format'
import SetlistForm from '../components/SetlistForm.vue'
import { useBandStore } from '@/features/bands/stores/bandStore'
import LoadingState from '@/shared/components/LoadingState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import SectionCard from '@/shared/components/SectionCard.vue'
import { getErrorMessage } from '@/shared/api/ApiError'
import { useAsyncAction } from '@/shared/composables/useAsyncAction'

const bands = useBandStore()
const router = useRouter()

const setlists = ref<SetlistSummary[]>([])
const isLoading = ref(true)
const error = ref<unknown>(null)

async function load() {
  isLoading.value = true
  error.value = null
  try {
    setlists.value = await setlistsApi.list()
  } catch (e) {
    error.value = e
  } finally {
    isLoading.value = false
  }
}
watch(() => bands.activeId, (id) => { if (id) void load() }, { immediate: true })

const creation = useAsyncAction(setlistsApi.create)

async function onCreate(input: SetlistInput) {
  const created = await creation.run(input)
  if (created) await router.push({ name: 'setlist-detail', params: { setlistId: created.id } })
}
</script>

<template>
  <section>
    <header class="page-header">
      <h1>Setlists</h1>
    </header>

    <SectionCard v-if="bands.can('write')" title="Nouvelle setlist">
      <SetlistForm submit-label="Créer la setlist" :submitting="creation.isLoading.value"
                   :error="creation.error.value ? getErrorMessage(creation.error.value) : null" @submit="onCreate" />
    </SectionCard>

    <LoadingState v-if="isLoading" />
    <ErrorState v-else-if="error" :message="getErrorMessage(error)" @retry="load" />
    <EmptyState v-else-if="setlists.length === 0" message="Aucune setlist pour l'instant." />

    <ul v-else class="setlist-list">
      <li v-for="setlist in setlists" :key="setlist.id" class="card" data-testid="setlist-item">
        <RouterLink :to="{ name: 'setlist-detail', params: { setlistId: setlist.id } }">{{ setlist.name }}</RouterLink>
        <p class="muted">
          {{ formatSetlistDate(setlist.date) }}<template v-if="setlist.venue"> · {{ setlist.venue }}</template> ·
          {{ setlist.itemCount }} {{ setlist.itemCount > 1 ? 'morceaux' : 'morceau' }}
        </p>
      </li>
    </ul>
  </section>
</template>

<style scoped>
.setlist-list { list-style: none; padding: 0; display: grid; gap: .5rem; }
.setlist-list p { margin: .2rem 0 0; }
</style>