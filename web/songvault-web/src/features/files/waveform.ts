/** #15 : dessin de la forme d'onde, sans bibliothèque (ADR 0021). */

export const WAVEFORM_HEIGHT = 40
const BAR_WIDTH = 0.6

/** Réduit les crêtes à `count` barres en gardant la plus haute de chaque groupe. */
export function waveformBars(peaks: number[], count: number): number[] {
  if (peaks.length === 0) return []
  const bars = Math.min(count, peaks.length)
  return Array.from({ length: bars }, (_, i) => {
    const from = Math.floor((i * peaks.length) / bars)
    const to = Math.max(from + 1, Math.floor(((i + 1) * peaks.length) / bars))
    return Math.max(...peaks.slice(from, to))
  })
}

/** Un seul chemin SVG pour toutes les barres (une barre par unité de largeur, centrées verticalement). */
export function waveformPath(bars: number[]): string {
  return bars.map((value, x) => {
    const h = Math.max(1, (value / 255) * WAVEFORM_HEIGHT)
    const y = (WAVEFORM_HEIGHT - h) / 2
    return `M${x} ${round(y)}h${BAR_WIDTH}v${round(h)}h-${BAR_WIDTH}z`
  }).join('')
}

const round = (n: number) => Math.round(n * 100) / 100