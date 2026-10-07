import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { songsApi } from '../api/songsApi'
import type { Song } from '../types'
import type { SongVersionStatus } from '@/features/versions/types'

/** Filtres de la liste : chaîne vide = pas de filtre. */
export interface SongFilters {
  search: string
  status: SongVersionStatus | ''
}

export const useSongsStore = defineStore('songs', () => {
  // ================= État =================
  const items = ref<Song[]>([])
  const isLoading = ref(false)
  const isLoaded = ref(false)
  const error = ref<unknown>(null)
  const filters = ref<SongFilters>({ search: '', status: '' })

  const count = computed(() => items.value.length)

  /**
   * Numéro de la dernière requête lancée.
   * Si une réponse arrive alors qu'une requête PLUS RÉCENTE est partie
   * (ex. « goo » qui revient après « goodbye »), elle est ignorée.
   */
  let lastRequest = 0

  // ================= Chargement =================

  /** Charge la liste avec les filtres courants. Sans `force`, le cache est réutilisé. */
  async function fetchList(force = false) {
    if (isLoaded.value && !force) return

    const requestId = ++lastRequest
    isLoading.value = true
    error.value = null

    try {
      const page = await songsApi.list({
        search: filters.value.search || undefined,
        status: filters.value.status || undefined,
      })
      if (requestId !== lastRequest) return          // réponse périmée : on l'ignore
      items.value = page.items
      isLoaded.value = true
    } catch (e) {
      if (requestId === lastRequest) error.value = e
    } finally {
      // Seule la requête la plus récente pilote l'indicateur de chargement
      if (requestId === lastRequest) isLoading.value = false
    }
  }

  /** Change les filtres et recharge. Ne fait rien si les filtres sont identiques et déjà chargés. */
  function setFilters(next: SongFilters): Promise<void> {
    const unchanged = next.search === filters.value.search && next.status === filters.value.status
    if (unchanged && isLoaded.value) return Promise.resolve()

    filters.value = { ...next }
    return fetchList(true)
  }

  // ================= Mises à jour locales (après création, modification, suppression) =================

  /** Ajoute ou remplace un morceau, puis retrie par titre (même ordre que l'API). */
  function upsert(song: Song) {
    const index = items.value.findIndex((s) => s.id === song.id)
    if (index >= 0) items.value[index] = song
    else items.value.push(song)
    items.value.sort((a, b) => a.title.localeCompare(b.title, 'fr'))
  }

  function remove(id: string) {
    items.value = items.value.filter((s) => s.id !== id)
  }

  /** Force un rechargement à la prochaine visite de la liste. */
  function invalidate() {
    isLoaded.value = false
  }

  /** Oublie tout (déconnexion) : la personne suivante ne doit rien voir. */
function reset() {
  lastRequest++                                   // ignore une éventuelle réponse encore en vol
  items.value = []
  filters.value = { search: '', status: '' }
  isLoaded.value = false
  isLoading.value = false
  error.value = null
}
  
  return { items, isLoading, isLoaded, error, filters, count, fetchList, setFilters, upsert, remove, invalidate, reset }
})