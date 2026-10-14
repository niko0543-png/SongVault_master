<script setup lang="ts">
import { ref, watch } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { releasesApi } from '../api/releasesApi'
import { releaseTypeLabels, type ReleaseInput, type ReleaseSummary } from '../types'
import { formatReleaseDate } from '../format'
import ReleaseForm from '../components/ReleaseForm.vue'
import { useBandStore } from '@/features/bands/stores/bandStore'
import LoadingState from '@/shared/components/LoadingState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import SectionCard from '@/shared/components/SectionCard.vue'
import { getErrorMessage } from '@/shared/api/ApiError'
import { useAsyncAction } from '@/shared/composables/useAsyncAction'

const bands = useBandStore()
const router = useRouter()

const releases = ref<ReleaseSummary[]>([])
const isLoading = ref(true)
const error = ref<unknown>(null)

async function load() {
  isLoading.value = true
  error.value = null
  try {
    releases.value = await releasesApi.list()
  } catch (e) {
    error.value = e
  } finally {
    isLoading.value = false
  }
}
watch(() => bands.activeId, (id) => { if (id) void load() }, { immediate: true })

const creation = useAsyncAction(releasesApi.create)

async function onCreate(input: ReleaseInput) {
  const created = await creation.run(input)
  if (created) await router.push({ name: 'release-detail', params: { releaseId: created.id } })
}
</script>

<template>
  <section>
    <header class="page-header">
      <h1>Sorties</h1>
    </header>

    <SectionCard v-if="bands.can('write')" title="Nouvelle sortie">
      <ReleaseForm submit-label="Créer la sortie" :submitting="creation.isLoading.value"
                   :error="creation.error.value ? getErrorMessage(creation.error.value) : null" @submit="onCreate" />
    </SectionCard>

    <LoadingState v-if="isLoading" />
    <ErrorState v-else-if="error" :message="getErrorMessage(error)" @retry="load" />
    <EmptyState v-else-if="releases.length === 0" message="Aucune sortie pour l'instant : ni single, ni EP, ni album." />

    <ul v-else class="release-list">
      <li v-for="release in releases" :key="release.id" class="card release" data-testid="release-item">
        <img v-if="release.hasCover" :src="releasesApi.coverUrl(release.id)" alt="" class="cover" loading="lazy" />
        <span v-else class="cover placeholder" aria-hidden="true">♪</span>
        <div>
          <RouterLink :to="{ name: 'release-detail', params: { releaseId: release.id } }">{{ release.title }}</RouterLink>
          <p class="muted">
            {{ releaseTypeLabels[release.type] }} · {{ formatReleaseDate(release.releaseDate) }} ·
            {{ release.trackCount }} {{ release.trackCount > 1 ? 'pistes' : 'piste' }}
          </p>
        </div>
      </li>
    </ul>
  </section>
</template>

<style scoped>
.release-list { list-style: none; padding: 0; display: grid; gap: .5rem; }
.release { display: grid; grid-template-columns: 56px 1fr; gap: .75rem; align-items: center; }
.release p { margin: .2rem 0 0; }
.cover { width: 56px; height: 56px; object-fit: cover; border-radius: var(--radius); }
.placeholder { display: grid; place-items: center; background: var(--bg); color: var(--muted); font-size: 1.5rem; }
</style>