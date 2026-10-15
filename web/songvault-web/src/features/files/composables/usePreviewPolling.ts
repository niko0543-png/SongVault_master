import { onBeforeUnmount, toValue, watch, type MaybeRefOrGetter } from 'vue'
import type { SongFile } from '../types'

export const PREVIEW_POLL_MS = 5000

/**
 * #15 : tant qu'un aperçu est en préparation, relit la version toutes les 5 secondes et ne recopie
 * QUE l'état des aperçus (le formulaire en cours de saisie n'est pas touché).
 */
export function usePreviewPolling(
  files: MaybeRefOrGetter<SongFile[] | undefined>,
  fetchFiles: () => Promise<SongFile[]>,
  onError?: (error: unknown) => void,
): { stop: () => void } {
  let timer: ReturnType<typeof setTimeout> | null = null
  let stopped = false

  const hasPending = () => (toValue(files) ?? []).some((f) => f.preview === 'Pending')

  function schedule() {
    if (timer !== null || stopped || !hasPending()) return
    timer = setTimeout(tick, PREVIEW_POLL_MS)
  }

  async function tick() {
    timer = null
    try {
      const fresh = new Map((await fetchFiles()).map((f) => [f.id, f.preview]))
      for (const file of toValue(files) ?? []) {
        if (fresh.has(file.id)) file.preview = fresh.get(file.id)
      }
    } catch (e) {
      onError?.(e)              // erreur passagère : on réessaiera au prochain tour
    }
    schedule()
  }

  function stop() {
    stopped = true
    if (timer !== null) clearTimeout(timer)
    timer = null
  }

  // Un envoi, une relance ou un changement de version peut faire réapparaître un aperçu en préparation
  watch(() => (toValue(files) ?? []).map((f) => f.preview).join(), schedule, { immediate: true })
  onBeforeUnmount(stop)
  return { stop }
}