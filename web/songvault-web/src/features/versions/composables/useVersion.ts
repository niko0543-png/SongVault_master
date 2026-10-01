import { computed, ref, toValue, watch, type MaybeRefOrGetter } from 'vue'
import { versionsApi } from '../api/versionsApi'
import type { SongVersion, SongVersionInput, SongVersionSummary } from '../types'
import { songsApi } from '@/features/songs/api/songsApi'
import type { Song } from '@/features/songs/types'

/** Charge une version, son morceau et ses voisines ; expose l'enregistrement. */
export function useVersion(songId: MaybeRefOrGetter<string>, versionId: MaybeRefOrGetter<string>) {
  const song = ref<Song | null>(null)
  const version = ref<SongVersion | null>(null)
  const siblings = ref<SongVersionSummary[]>([])
  const isLoading = ref(true)
  const error = ref<unknown>(null)
  const isSaving = ref(false)
  const saveError = ref<unknown>(null)

  async function load() {
    isLoading.value = true
    error.value = null
    try {
      const sId = toValue(songId)
      ;[song.value, version.value, siblings.value] = await Promise.all([
        songsApi.get(sId),
        versionsApi.get(sId, toValue(versionId)),
        versionsApi.list(sId),
      ])
    } catch (e) {
      error.value = e
    } finally {
      isLoading.value = false
    }
  }

  async function save(input: SongVersionInput) {
    const current = version.value
    if (!current) return
    isSaving.value = true
    saveError.value = null
    try {
      await versionsApi.update(toValue(songId), current.id, input)
      version.value = await versionsApi.get(toValue(songId), current.id)   // l'état du SERVEUR fait foi
      siblings.value = siblings.value.map((s) =>
        s.id === current.id ? { ...s, title: input.title, status: input.status } : s)
    } catch (e) {
      saveError.value = e
    } finally {
      isSaving.value = false
    }
  }

  // Les versions arrivent triées par numéro croissant (ORDER BY côté API)
  const previous = computed(() => {
    const n = version.value?.number
    return n === undefined ? null : ([...siblings.value].reverse().find((s) => s.number < n) ?? null)
  })
  const next = computed(() => {
    const n = version.value?.number
    return n === undefined ? null : (siblings.value.find((s) => s.number > n) ?? null)
  })

  // Recharge quand l'un des deux paramètres change (version précédente/suivante)
  watch(() => [toValue(songId), toValue(versionId)], () => load(), { immediate: true })

  return { song, version, siblings, previous, next, isLoading, error, isSaving, saveError, save, reload: load }
}
