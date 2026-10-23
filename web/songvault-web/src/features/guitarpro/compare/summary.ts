import { t } from '@/shared/i18n'
import { describeGlobal } from './describe'
import type { BarChange, ScoreDiff } from './scoreDiff'

/**
 * #36 : les différences en quelques lignes, pour le résumé à côté du fichier dans l'écran A/B :
 * d'abord les changements d'ensemble (tempo, pistes, accordage, capodastre), puis une ligne par piste modifiée.
 * Le détail, mesure par mesure, est sur la page de la tablature (« Afficher les modifs »).
 */
export function summarize(diff: ScoreDiff): string[] {
  const lines = diff.global.map(describeGlobal)
  for (const track of diff.tracks) {
    const count = (kind: BarChange['kind']) => track.changes.filter((change) => change.kind === kind).length
    const details = [
      ['modified', 'guitarpro.compare.summary.barsModified'],
      ['added', 'guitarpro.compare.summary.barsAdded'],
      ['removed', 'guitarpro.compare.summary.barsRemoved'],
    ] as const
    const parts = details.filter(([kind]) => count(kind) > 0).map(([kind, key]) => t(key, { count: count(kind) }))
    if (parts.length > 0) lines.push(t('guitarpro.compare.summary.track', { track: track.name, details: parts.join(', ') }))
  }
  return lines
}
