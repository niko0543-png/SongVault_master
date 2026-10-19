/**
 * Moteur du mélangeur (#23, ADR 0030). Toutes les pistes démarrent au même instant de l'horloge audio, avec
 * le même décalage : elles restent calées à l'échantillon près, quelle que soit la durée d'écoute.
 * Chaque piste passe par son GainNode (muet, solo, volume), puis par un gain général.
 *
 * Ce module n'importe rien : le test de dérive (e2e/stems-drift.spec.ts) le charge tel quel dans Chromium.
 */

/** Délai avant le départ : le temps de créer toutes les sources, pour qu'aucune ne parte en retard. */
export const START_DELAY_SECONDS = 0.1
/** Un changement de gain se fait en ~15 ms, sans clic audible. */
export const GAIN_SMOOTHING_SECONDS = 0.015

export interface Mixer {
  /** Durée de la piste la plus longue, en secondes. */
  readonly duration: number
  readonly isPlaying: boolean
  /** Lance toutes les pistes à offset secondes ; rend l'instant de l'horloge audio où elles partent. */
  start(offset: number): number
  /** Arrête toutes les pistes en gardant la position. */
  stop(): void
  /** Un gain par piste, dans l'ordre des pistes (0 = muette). */
  setGains(gains: readonly number[]): void
  /** Position de lecture, en secondes. */
  position(): number
  /** Arrête tout et débranche les nœuds audio. */
  dispose(): void
}

export function createMixer(ctx: BaseAudioContext, buffers: readonly AudioBuffer[]): Mixer {
  const master = ctx.createGain()
  master.connect(ctx.destination)
  const gains = buffers.map(() => {
    const node = ctx.createGain()
    node.connect(master)
    return node
  })
  const duration = buffers.reduce((longest, b) => Math.max(longest, b.duration), 0)

  let sources: AudioBufferSourceNode[] = []
  let origin = 0          // instant de l'horloge audio qui correspond à la position 0
  let startOffset = 0
  let pausedAt = 0
  let playing = false

  function silence() {
    for (const source of sources) {
      source.onended = null
      source.stop()
      source.disconnect()
    }
    sources = []
  }

  function position() {
    if (!playing) return pausedAt
    return Math.min(duration, Math.max(startOffset, ctx.currentTime - origin))
  }

  function start(offset: number) {
    silence()
    const from = Math.min(Math.max(0, offset), duration)
    const when = ctx.currentTime + START_DELAY_SECONDS
    buffers.forEach((buffer, i) => {
      if (from >= buffer.duration) return              // piste plus courte, déjà finie à cette position
      const source = ctx.createBufferSource()
      source.buffer = buffer
      source.connect(gains[i]!)
      source.start(when, from)
      sources.push(source)
    })

    let remaining = sources.length
    for (const source of sources) {
      source.onended = () => {
        if (--remaining > 0) return
        sources = []
        playing = false
        pausedAt = 0                                   // fin du morceau : la prochaine lecture repart du début
      }
    }
    origin = when - from
    startOffset = from
    playing = sources.length > 0
    pausedAt = playing ? from : 0
    return when
  }

  function stop() {
    if (!playing) return
    pausedAt = position()
    playing = false
    silence()
  }

  function setGains(values: readonly number[]) {
    const now = ctx.currentTime
    gains.forEach((node, i) => {
      node.gain.cancelScheduledValues(now)
      node.gain.setTargetAtTime(values[i] ?? 1, now, GAIN_SMOOTHING_SECONDS)
    })
  }

  function dispose() {
    silence()
    playing = false
    for (const node of gains) node.disconnect()
    master.disconnect()
  }

  return {
    duration,
    get isPlaying() { return playing },
    start, stop, setGains, position, dispose,
  }
}