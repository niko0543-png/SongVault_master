import type { AlphaTabApi, synth } from '@coderline/alphatab'

/**
 * #29 : branche un élément <audio> sur le lecteur d'alphaTab en mode « média externe » (PlayerMode.EnabledExternalMedia).
 * alphaTab commande l'élément (lecture, pause, déplacement, vitesse), et l'élément lui donne l'heure : à chaque image
 * pendant la lecture, et après chaque déplacement. alphaTab en déduit, par les points de synchronisation, l'endroit
 * de la partition. Rend la fonction qui débranche tout.
 */
export function connectExternalMedia(api: AlphaTabApi, audio: HTMLMediaElement): () => void {
  const output = api.player?.output as synth.IExternalMediaSynthOutput | undefined
  if (!output) return () => {}

  // alphaTab peut demander plusieurs déplacements d'affilée (au branchement, au chargement des points) : seul le
  // dernier est appliqué, à la fin de la tâche en cours. Des déplacements qui se chevauchent bloquent parfois
  // l'élément <audio> de Chromium en attente de données (« waiting ») quand la machine est chargée.
  let seekTarget: number | null = null
  const seek = (seconds: number) => {
    const first = seekTarget === null
    seekTarget = seconds
    if (!first) return
    queueMicrotask(() => {
      if (seekTarget !== null && Math.abs(audio.currentTime - seekTarget) > 0.001) audio.currentTime = seekTarget
      seekTarget = null
    })
  }

  output.handler = {
    get backingTrackDuration() { return Number.isFinite(audio.duration) ? audio.duration * 1000 : 0 },
    get playbackRate() { return audio.playbackRate },
    set playbackRate(value: number) { audio.playbackRate = value },
    get masterVolume() { return audio.volume },
    set masterVolume(value: number) { audio.volume = value },
    seekTo(time: number) { seek(time / 1000) },
    play() { void audio.play().catch(() => api.pause()) },          // lecture refusée par le navigateur : on se remet en pause
    pause() { audio.pause() },
  }

  let frame = 0
  const update = () => output.updatePosition(audio.currentTime * 1000)
  const tick = () => {
    update()
    frame = audio.paused ? 0 : requestAnimationFrame(tick)
  }
  const onPlay = () => { if (!frame) frame = requestAnimationFrame(tick) }
  const events: [string, () => void][] = [['play', onPlay], ['seeked', update], ['pause', update], ['ended', update]]
  for (const [type, listener] of events) audio.addEventListener(type, listener)

  return () => {
    cancelAnimationFrame(frame)
    frame = 0
    for (const [type, listener] of events) audio.removeEventListener(type, listener)
    output.handler = undefined
  }
}