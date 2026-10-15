import { onUnmounted, watch } from 'vue'
import { storeToRefs } from 'pinia'
import { usePlayerStore } from '../stores/playerStore'

/** Pas de 10 s des boutons « reculer » et « avancer », ici et dans le lecteur. */
export const SKIP_SECONDS = 10

/**
 * #14 : titre et commandes de la piste sur l'écran verrouillé et dans le centre de notifications
 * (Android, iOS, Windows, macOS), via l'API Media Session. Sans elle, le téléphone verrouillé n'affiche rien.
 */
export function useMediaSession(session: MediaSession | undefined = globalThis.navigator?.mediaSession) {
  if (!session) return                                        // navigateur sans Media Session : rien à faire
  const player = usePlayerStore()
  const { track, isPlaying, currentTime, duration } = storeToRefs(player)

  const actions: [MediaSessionAction, MediaSessionActionHandler][] = [
    ['play', () => void player.toggle()],
    ['pause', () => player.pause()],
    ['seekbackward', (d) => player.skip(-(d.seekOffset ?? SKIP_SECONDS))],
    ['seekforward', (d) => player.skip(d.seekOffset ?? SKIP_SECONDS)],
    ['seekto', (d) => { if (d.seekTime !== undefined) player.seek(d.seekTime) }],
  ]
  for (const [action, handler] of actions) {
    try {
      session.setActionHandler(action, handler)
    } catch {
      // action inconnue de ce navigateur (Safari ancien) : on passe à la suivante
    }
  }

  watch(track, (next) => {
    session.metadata = next
      ? new MediaMetadata({
          title: next.title,
          artist: next.subtitle,
          album: 'SongVault',
          artwork: [
            { src: '/icons/icon-192.png', sizes: '192x192', type: 'image/png' },
            { src: '/icons/icon-512.png', sizes: '512x512', type: 'image/png' },
          ],
        })
      : null
  }, { immediate: true })

  watch(isPlaying, (playing) => {
    session.playbackState = track.value ? (playing ? 'playing' : 'paused') : 'none'
  }, { immediate: true })

  // Barre de progression de l'écran verrouillé ; ignorée tant que la durée est inconnue
  watch([currentTime, duration], ([position, total]) => {
    if (!total || !Number.isFinite(total) || !session.setPositionState) return
    try {
      session.setPositionState({ duration: total, position: Math.min(position, total), playbackRate: 1 })
    } catch {
      // valeurs refusées pendant un changement de piste : la prochaine mise à jour corrigera
    }
  })

  onUnmounted(() => {
    for (const [action] of actions) {
      try { session.setActionHandler(action, null) } catch { /* idem */ }
    }
    session.metadata = null
  })
}