import { t } from '@/shared/i18n'
import type { BarChange, GlobalChange } from './scoreDiff'

/** #30 : les différences en phrases, pour la liste (la référence, la couleur ne fait que la doubler). */

const NAMES = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B']

/** Accordage alphaTab (du plus aigu au plus grave, en MIDI) → « E A D G B E », de la corde grave à l'aiguë. */
export function tuningName(tuning: number[]): string {
  return [...tuning].reverse().map((pitch) => NAMES[pitch % 12]).join(' ')
}

const capo = (fret: number) => (fret === 0 ? t('guitarpro.compare.noCapo') : t('guitarpro.compare.capoFret', { fret }))

export function describeGlobal(change: GlobalChange): string {
  switch (change.kind) {
    case 'tempo': return t('guitarpro.compare.tempo', { before: change.before, after: change.after })
    case 'trackAdded': return t('guitarpro.compare.trackAdded', { track: change.track })
    case 'trackRemoved': return t('guitarpro.compare.trackRemoved', { track: change.track })
    case 'tuning': return t('guitarpro.compare.tuning', { track: change.track, before: tuningName(change.before), after: tuningName(change.after) })
    case 'capo': return t('guitarpro.compare.capo', { track: change.track, before: capo(change.before), after: capo(change.after) })
  }
}

/** Les numéros de mesure commencent à 1, comme dans Guitar Pro. `barCount` : nombre de mesures de B. */
export function describeBar(change: BarChange, barCount: number): string {
  if (change.kind === 'added') return t('guitarpro.compare.added', { bar: change.barB + 1 })
  if (change.kind === 'removed') {
    return change.beforeB >= barCount
      ? t('guitarpro.compare.removedAtEnd', { bar: change.barA + 1 })
      : t('guitarpro.compare.removed', { bar: change.barA + 1, before: change.beforeB + 1 })
  }
  const details: string[] = []
  if (change.notes.length > 0) details.push(t('guitarpro.compare.changedNotes', { count: change.notes.length }))
  if (change.removedNotes > 0) details.push(t('guitarpro.compare.removedNotes', { count: change.removedNotes }))
  if (details.length === 0) details.push(t('guitarpro.compare.otherVoice'))
  return t('guitarpro.compare.modified', { bar: change.barB + 1, details: details.join(', ') })
}

/**
 * #37 : la mesure de A qui correspond à une différence, pour placer l'ancienne partition.
 * Une mesure ajoutée n'existe pas dans A : c'est la mesure de A qui suit l'endroit où elle a été insérée.
 * `changes` : les différences de la piste, dans l'ordre ; `barCountA` : nombre de mesures de A.
 */
export function targetBarA(change: BarChange, changes: BarChange[], barCountA: number): number {
  if (change.kind !== 'added') return change.barA
  const added = changes.filter((c) => c.kind === 'added' && c.barB < change.barB).length
  const removed = changes.filter((c) => c.kind === 'removed' && c.beforeB <= change.barB).length
  return Math.max(0, Math.min(change.barB - added + removed, barCountA - 1))
}

/**
 * #37 : la mesure qui correspond à `bar` dans l'autre partition, d'après les différences de la piste :
 * de B vers A (`toOld`) ou de A vers B. Une mesure qui n'existe que d'un côté renvoie à la suivante de l'autre.
 */
export function matchingBar(bar: number, changes: BarChange[], toOld: boolean, barCountA: number, barCountB: number): number {
  const clamp = (value: number, count: number) => Math.max(0, Math.min(value, count - 1))
  const toA = (b: number) => b - changes.filter((c) => c.kind === 'added' && c.barB < b).length
    + changes.filter((c) => c.kind === 'removed' && c.beforeB <= b).length
  if (toOld) return clamp(toA(bar), barCountA)
  const removed = changes.find((c) => c.kind === 'removed' && c.barA === bar)
  if (removed?.kind === 'removed') return clamp(removed.beforeB, barCountB)
  for (let b = 0; b < barCountB; b++) {
    const added = changes.some((c) => c.kind === 'added' && c.barB === b)
    if (!added && toA(b) >= bar) return b
  }
  return clamp(barCountB - 1, barCountB)
}

/** La mesure de B où placer la partition pour une différence. */
export function targetBar(change: BarChange, barCount: number): number {
  return change.kind === 'removed' ? Math.min(change.beforeB, barCount - 1) : change.barB
}