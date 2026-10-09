<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'
import { useSongsStore } from '../stores/songsStore'
import { useBandStore } from '@/features/bands/stores/bandStore'
import SongCard from '../components/SongCard.vue'
import SearchBar from '../components/SearchBar.vue'
import LoadingState from '@/shared/components/LoadingState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import { getErrorMessage } from '@/shared/api/ApiError'
import { useDebouncedRef } from '@/shared/composables/useDebouncedRef'
import { SONG_VERSION_STATUSES, type SongVersionStatus } from '@/features/versions/types'

const route = useRoute()
const router = useRouter()
const store = useSongsStore()
const { items, isLoading, isLoaded, error } = storeToRefs(store)   // réactif
const bands = useBandStore()

// ================= Filtres : l'URL fait foi =================

/** Accepte uniquement un statut connu ; toute autre valeur de l'URL est ignorée. */
function statusFromQuery(value: unknown): SongVersionStatus | '' {
  return SONG_VERSION_STATUSES.find((s) => s === value) ?? ''
}

function searchFromQuery(value: unknown): string {
  return typeof value === 'string' ? value : ''
}

// Valeurs initiales lues dans l'URL (lien partagé, F5)
const search = ref(searchFromQuery(route.query.search))
const status = ref<SongVersionStatus | ''>(statusFromQuery(route.query.status))

// La recherche n'interroge l'API qu'après 300 ms sans frappe ; le statut, lui, est immédiat
const debouncedSearch = useDebouncedRef(search, 300)

const hasFilters = computed(() => search.value.trim() !== '' || status.value !== '')

const emptyMessage = computed(() =>
  hasFilters.value ? 'Aucun morceau ne correspond à la recherche.' : "Aucun morceau pour l'instant.")

// Filtres → URL + chargement
watch([debouncedSearch, status], ([s, st]) => {
  const term = s.trim()
  void router.replace({
    query: {
      ...(term ? { search: term } : {}),
      ...(st ? { status: st } : {}),
    },
  })
  void store.setFilters({ search: term, status: st })
})

// URL → filtres : si l'URL change sans que la page soit recréée
// (ex. clic sur « SongVault » dans l'en-tête, qui ramène à /songs sans paramètres)
watch(
  () => route.query,
  (query) => {
    const s = searchFromQuery(query.search)
    const st = statusFromQuery(query.status)
    if (s !== search.value.trim()) search.value = s
    if (st !== status.value) status.value = st
  },
)

function clearFilters() {
  search.value = ''
  status.value = ''
}

// ================= Navigation =================

function onSelect(id: string) {
  void router.push({ name: 'song-detail', params: { songId: id } })
}

// ================= Premier chargement =================

onMounted(() => {
  void store.setFilters({ search: search.value.trim(), status: status.value })
})
</script>

<template>
  <section>
    <header class="page-header">
      <h1>Morceaux</h1>
        <RouterLink v-if="bands.can('write')" :to="{ name: 'song-create' }">+ Nouveau morceau</RouterLink>
    </header>

    <SearchBar v-model:search="search" v-model:status="status" />

    <LoadingState v-if="isLoading || (!isLoaded && !error)" />

    <ErrorState v-else-if="error" :message="getErrorMessage(error)" @retry="store.fetchList(true)" />

    <EmptyState v-else-if="items.length === 0" :message="emptyMessage">
      <button v-if="hasFilters" type="button" class="secondary" @click="clearFilters">
        Effacer les filtres
      </button>
       <RouterLink v-else-if="bands.can('write')" :to="{ name: 'song-create' }">Créer le premier morceau</RouterLink>
    </EmptyState>

    <template v-else>
      <p class="muted" role="status">
        <small>{{ items.length }} morceau{{ items.length > 1 ? 'x' : '' }}</small>
      </p>
      <ul class="song-list">
        <li v-for="song in items" :key="song.id">
          <SongCard :song="song" @select="onSelect" />
        </li>
      </ul>
    </template>
  </section>
</template>

<style scoped>
.song-list { list-style: none; padding: 0; display: grid; gap: .75rem; }
</style>