import type { PlainBar, PlainScore, PlainTrack } from './plain'
import { diffSequence, pairChanges } from './sequence'

/**
 * #30 : différences entre deux partitions, de A (avant) vers B (après), comme les versions (#9).
 * D'abord les changements d'ensemble (tempo, pistes, accordage, capodastre), annoncés en une ligne
 * chacun ; puis, piste par piste, les mesures ajoutées, supprimées ou modifiées, note par note.
 */

/** Une note de B, par sa position : mesure, temps (première voix de la première portée), note. */
export interface NoteRef {
  bar: number
  beat: number
  note: number
}

export type GlobalChange =
  | { kind: 'tempo'; before: number; after: number }
  | { kind: 'trackAdded'; track: string }
  | { kind: 'trackRemoved'; track: string }
  | { kind: 'tuning'; track: string; before: number[]; after: number[] }
  | { kind: 'capo'; track: string; before: number; after: number }

export type BarChange =
  /** Mesure de B qui n'existe pas dans A : toutes ses notes sont nouvelles. */
  | { kind: 'added'; barB: number }
  /** Mesure de A disparue ; elle se trouvait juste avant la mesure `beforeB` de B (B.length = à la fin). */
  | { kind: 'removed'; barA: number; beforeB: number }
  /** Mesure retouchée : `notes` sont les notes de B nouvelles ou changées, `removedNotes` le nombre de notes de A disparues. */
  | { kind: 'modified'; barA: number; barB: number; notes: NoteRef[]; removedNotes: number }

export interface TrackDiff {
  name: string
  trackA: number
  trackB: number
  changes: BarChange[]
}

export interface ScoreDiff {
  global: GlobalChange[]
  tracks: TrackDiff[]
}

const normalize = (name: string) => name.trim().toLocaleLowerCase('fr')

/**
 * Associe les pistes : même nom et même instrument, puis même nom, puis même instrument, dans l'ordre.
 * Une piste renommée garde ainsi ses différences ; une piste sans partenaire est ajoutée ou supprimée.
 */
export function matchTracks(a: PlainTrack[], b: PlainTrack[]): [PlainTrack, PlainTrack][] {
  const pairs: [PlainTrack, PlainTrack][] = []
  const freeA = new Set(a)
  const freeB = new Set(b)
  const rules: ((x: PlainTrack, y: PlainTrack) => boolean)[] = [
    (x, y) => normalize(x.name) === normalize(y.name) && x.program === y.program,
    (x, y) => normalize(x.name) === normalize(y.name),
    (x, y) => x.program === y.program,
  ]
  for (const rule of rules) {
    for (const x of a) {
      if (!freeA.has(x)) continue
      const y = b.find((candidate) => freeB.has(candidate) && rule(x, candidate))
      if (!y) continue
      pairs.push([x, y])
      freeA.delete(x)
      freeB.delete(y)
    }
  }
  return pairs.sort(([, x], [, y]) => x.index - y.index)
}

/** Notes d'une mesure modifiée : temps par temps, puis note par note dans les temps retouchés. */
function diffBar(barA: PlainBar, barB: PlainBar): { notes: NoteRef[]; removedNotes: number } {
  const notes: NoteRef[] = []
  let removedNotes = 0
  const beatsA = barA.beats
  const beatsB = barB.beats
  for (const change of pairChanges(diffSequence(beatsA.map((x) => x.key), beatsB.map((x) => x.key)))) {
    if (change.kind === 'added') {
      beatsB[change.b]!.notes.forEach((_, note) => notes.push({ bar: barB.index, beat: change.b, note }))
    } else if (change.kind === 'removed') {
      removedNotes += beatsA[change.a]!.notes.length
    } else {
      const before = beatsA[change.a]!
      const after = beatsB[change.b]!
      const keyOf = (n: { string: number; fret: number; effects: string }) => `${n.string}:${n.fret}:${n.effects}`
      const keysA = new Set(before.notes.map(keyOf))
      const keysB = new Set(after.notes.map(keyOf))
      const rhythmChanged = before.rhythm !== after.rhythm
      after.notes.forEach((n, note) => {
        if (rhythmChanged || !keysA.has(keyOf(n))) notes.push({ bar: barB.index, beat: change.b, note })
      })
      removedNotes += before.notes.filter((n) => !keysB.has(keyOf(n))).length
    }
  }
  return { notes, removedNotes }
}

export function diffTrack(a: PlainTrack, b: PlainTrack): BarChange[] {
  const changes = pairChanges(diffSequence(a.bars.map((x) => x.key), b.bars.map((x) => x.key)))
  return changes
    .map((change): BarChange => {
      if (change.kind === 'added') return { kind: 'added', barB: change.b }
      if (change.kind === 'removed') return { kind: 'removed', barA: change.a, beforeB: change.before }
      return { kind: 'modified', barA: change.a, barB: change.b, ...diffBar(a.bars[change.a]!, b.bars[change.b]!) }
    })
    .sort((x, y) => position(x) - position(y))
}

/** Où la différence se voit dans B : la mesure de B, ou celle devant laquelle une mesure a disparu. */
export function position(change: BarChange): number {
  return change.kind === 'removed' ? change.beforeB - 0.5 : change.barB
}

const sameNumbers = (x: number[], y: number[]) => x.length === y.length && x.every((v, i) => v === y[i])

export function diffScores(a: PlainScore, b: PlainScore): ScoreDiff {
  const global: GlobalChange[] = []
  if (a.tempo !== b.tempo) global.push({ kind: 'tempo', before: a.tempo, after: b.tempo })

  const pairs = matchTracks(a.tracks, b.tracks)
  const pairedA = new Set(pairs.map(([x]) => x))
  const pairedB = new Set(pairs.map(([, y]) => y))
  for (const x of a.tracks) if (!pairedA.has(x)) global.push({ kind: 'trackRemoved', track: x.name })
  for (const y of b.tracks) if (!pairedB.has(y)) global.push({ kind: 'trackAdded', track: y.name })

  const tracks: TrackDiff[] = []
  for (const [x, y] of pairs) {
    // L'accordage et le capodastre ne changent pas les cordes et les cases jouées : une ligne, pas une note colorée
    if (!sameNumbers(x.tuning, y.tuning)) global.push({ kind: 'tuning', track: y.name, before: x.tuning, after: y.tuning })
    if (x.capo !== y.capo) global.push({ kind: 'capo', track: y.name, before: x.capo, after: y.capo })
    tracks.push({ name: y.name, trackA: x.index, trackB: y.index, changes: diffTrack(x, y) })
  }
  return { global, tracks }
}