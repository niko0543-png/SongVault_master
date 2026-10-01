import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { songsApi } from '../api/songsApi'
import type { Song } from '../types'

export const useSongsStore = defineStore('songs', () => {
  const items = ref<Song[]>([])
  const isLoading = ref(false)
  const isLoaded = ref(false)
  const error = ref<unknown>(null)

  const count = computed(() => items.value.length)

  async function fetchList(force = false) {
    if ((isLoaded.value && !force) || isLoading.value) return   // cache : pas de rechargement inutile
    isLoading.value = true
    error.value = null
    try {
      items.value = (await songsApi.list()).items
      isLoaded.value = true
    } catch (e) {
      error.value = e
    } finally {
      isLoading.value = false
    }
  }

  /** Ajoute ou remplace un morceau après création ou modification. */
  function upsert(song: Song) {
    const index = items.value.findIndex((s) => s.id === song.id)
    if (index >= 0) items.value[index] = song
    else items.value.push(song)
    items.value.sort((a, b) => a.title.localeCompare(b.title, 'fr'))   // même ordre que l'API
  }

  function remove(id: string) {
    items.value = items.value.filter((s) => s.id !== id)
  }

  /** Force un rechargement à la prochaine visite de la liste. */
  function invalidate() {
    isLoaded.value = false
  }

  return { items, isLoading, isLoaded, error, count, fetchList, upsert, remove, invalidate }
})
