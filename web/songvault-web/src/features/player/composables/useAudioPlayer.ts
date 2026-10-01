import { onMounted, onUnmounted, type ShallowRef } from 'vue'
import { usePlayerStore } from '../stores/playerStore'

/** Relie l'élément <audio> du template au store, et le détache proprement. */
export function useAudioPlayer(element: Readonly<ShallowRef<HTMLAudioElement | null>>) {
  const player = usePlayerStore()
  onMounted(() => {
    if (element.value) player.attach(element.value)
  })
  onUnmounted(() => player.detach())   // retire les écouteurs : pas de fuite mémoire
}
