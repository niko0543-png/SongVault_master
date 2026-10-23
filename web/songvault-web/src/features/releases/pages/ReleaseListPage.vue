<script setup lang="ts">
import { ref, watch } from 'vue'
import { RouterLink } from 'vue-router'
import { releasesApi } from '../api/releasesApi'
import { releaseTypeLabels, type ReleaseSummary } from '../types'
import { formatReleaseDate } from '../format'
import { useBandStore } from '@/features/bands/stores/bandStore'
import LoadingState from '@/shared/components/LoadingState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { getErrorMessage } from '@/shared/api/ApiError'
import { t } from '@/shared/i18n'

const bands = useBandStore()

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

</script>

<template>
  <section>
    <header class="page-header">
      <h1>{{ t('releases.list.title') }}</h1>
    </header>

    <!-- #39 : une sortie naît de la section « Sortie » de la dernière version finale d'un morceau -->
    <p class="muted"><small>{{ t('releases.list.howTo') }}</small></p>

    <LoadingState v-if="isLoading" />
    <ErrorState v-else-if="error" :message="getErrorMessage(error)" @retry="load" />
    <EmptyState v-else-if="releases.length === 0" :message="t('releases.list.empty')" />

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