/** Réglages d'une piste du mélangeur (#23). */
export interface TrackControl {
  muted: boolean
  solo: boolean
  /** De 0 à 1. */
  volume: number
}

export function newControl(): TrackControl {
  return { muted: false, solo: false, volume: 1 }
}

/**
 * Le gain de chaque piste : une piste muette se tait, même en solo ; dès qu'une piste est en solo,
 * les pistes qui ne le sont pas se taisent ; sinon, le volume de la piste.
 */
export function effectiveGains(controls: readonly TrackControl[]): number[] {
  const anySolo = controls.some((c) => c.solo)
  return controls.map((c) => (c.muted || (anySolo && !c.solo) ? 0 : Math.min(1, Math.max(0, c.volume))))
}