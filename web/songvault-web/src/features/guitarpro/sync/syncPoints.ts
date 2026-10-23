import type { PlayedBar, TabSyncPoint } from './types'

/**
 * #29 : les mesures dans l'ordre où elles sont jouées, reprises comprises, à partir de la table de temps d'alphaTab
 * (api.tickCache.masterBars, triée par instant de lecture).
 */
export function playOrder(masterBars: readonly { masterBar: { index: number } }[]): PlayedBar[] {
  const seen = new Map<number, number>()
  return masterBars.map(({ masterBar }) => {
    const occurrence = seen.get(masterBar.index) ?? 0
    seen.set(masterBar.index, occurrence + 1)
    return { barIndex: masterBar.index, occurrence }
  })
}

/**
 * Les points d'un calage « au tapé » : la tape n° i marque le début de la i-ième mesure jouée. Les tapes en trop
 * (après la dernière mesure) sont ignorées, comme une tape qui ne vient pas après la précédente (double appui).
 */
export function pointsFromTaps(order: readonly PlayedBar[], tapsMs: readonly number[]): TabSyncPoint[] {
  const points: TabSyncPoint[] = []
  for (const tap of tapsMs) {
    const bar = order[points.length]
    if (!bar) break
    const ms = Math.round(tap)
    const previous = points[points.length - 1]
    if (ms < 0 || (previous && ms <= previous.millisecondOffset)) continue
    points.push({ barIndex: bar.barIndex, barOccurence: bar.occurrence, barPosition: 0, millisecondOffset: ms })
  }
  return points
}