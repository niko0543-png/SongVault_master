import type { SongFile } from '@/features/files/types'
import { statusLabels, type SongVersion } from './types'

// Différences entre deux versions (#9) : de A (avant) vers B (après).

export type LineKind = 'same' | 'added' | 'removed'

export interface DiffLine {
  kind: LineKind
  text: string
}

/** Au-delà, on ne calcule plus la plus longue sous-suite commune (mémoire en lignes A × lignes B). */
export const MAX_DIFF_CELLS = 4_000_000

function toLines(text: string | null): string[] {
  if (!text) return []
  return text.replace(/\r\n?/g, '\n').split('\n')
}

/**
 * Différences ligne à ligne : les lignes communes, puis ajoutées ou supprimées, dans l'ordre du texte.
 * Plus longue sous-suite commune ; dans un même bloc modifié, les suppressions précèdent les ajouts.
 */
export function diffLines(before: string | null, after: string | null): DiffLine[] {
  const a = toLines(before)
  const b = toLines(after)

  // Début et fin communs : rien à calculer, et la table reste petite
  let start = 0
  while (start < a.length && start < b.length && a[start] === b[start]) start++
  let endA = a.length
  let endB = b.length
  while (endA > start && endB > start && a[endA - 1] === b[endB - 1]) {
    endA--
    endB--
  }

  const head: DiffLine[] = a.slice(0, start).map((text) => ({ kind: 'same', text }))
  const tail: DiffLine[] = a.slice(endA).map((text) => ({ kind: 'same', text }))
  const midA = a.slice(start, endA)
  const midB = b.slice(start, endB)
  return [...head, ...diffMiddle(midA, midB), ...tail]
}

function diffMiddle(a: string[], b: string[]): DiffLine[] {
  const removed = (text: string): DiffLine => ({ kind: 'removed', text })
  const added = (text: string): DiffLine => ({ kind: 'added', text })
  if (a.length === 0 || b.length === 0 || a.length * b.length > MAX_DIFF_CELLS) {
    return [...a.map(removed), ...b.map(added)]
  }

  // lcs[i][j] = longueur de la plus longue sous-suite commune de a[i..] et b[j..]
  const width = b.length + 1
  const lcs = new Uint32Array((a.length + 1) * width)
  for (let i = a.length - 1; i >= 0; i--) {
    for (let j = b.length - 1; j >= 0; j--) {
      lcs[i * width + j] = a[i] === b[j]
        ? lcs[(i + 1) * width + j + 1]! + 1
        : Math.max(lcs[(i + 1) * width + j]!, lcs[i * width + j + 1]!)
    }
  }

  const result: DiffLine[] = []
  let i = 0
  let j = 0
  while (i < a.length && j < b.length) {
    if (a[i] === b[j]) {
      result.push({ kind: 'same', text: a[i]! })
      i++
      j++
    } else if (lcs[(i + 1) * width + j]! >= lcs[i * width + j + 1]!) {
      result.push(removed(a[i++]!))
    } else {
      result.push(added(b[j++]!))
    }
  }
  return [...result, ...a.slice(i).map(removed), ...b.slice(j).map(added)]
}

/** Nombre de lignes ajoutées et supprimées. */
export function countChanges(lines: DiffLine[]): { added: number; removed: number } {
  return {
    added: lines.filter((l) => l.kind === 'added').length,
    removed: lines.filter((l) => l.kind === 'removed').length,
  }
}

/** Index des lignes qui ouvrent un bloc de changements (pour aller d'un changement à l'autre). */
export function changeStarts(lines: DiffLine[]): number[] {
  return lines.flatMap((line, i) =>
    line.kind !== 'same' && (i === 0 || lines[i - 1]!.kind === 'same') ? [i] : [])
}

export interface FieldDiff {
  key: 'title' | 'status' | 'bpm' | 'key'
  label: string
  before: string
  after: string
  changed: boolean
}

/** Champs courts d'une version, tels qu'affichés, avec « — » pour une valeur absente. */
export function diffFields(a: SongVersion, b: SongVersion): FieldDiff[] {
  const show = (value: string | number | null) => (value === null || value === '' ? '—' : String(value))
  const fields: [FieldDiff['key'], string, (v: SongVersion) => string][] = [
    ['title', 'Titre', (v) => show(v.title)],
    ['status', 'Statut', (v) => statusLabels[v.status]],
    ['bpm', 'BPM', (v) => show(v.bpm)],
    ['key', 'Tonalité', (v) => show(v.key)],
  ]
  return fields.map(([key, label, read]) => {
    const before = read(a)
    const after = read(b)
    return { key, label, before, after, changed: before !== after }
  })
}

export interface FilesDiff {
  added: SongFile[]
  removed: SongFile[]
  kept: SongFile[]
}

/**
 * Fichiers ajoutés, retirés ou gardés de A à B. L'API ne donne pas d'empreinte : deux fichiers
 * sont « le même » s'ils portent le même nom, sans tenir compte de la casse.
 */
export function diffFiles(a: SongFile[], b: SongFile[]): FilesDiff {
  const name = (f: SongFile) => f.originalFileName.toLocaleLowerCase('fr')
  const namesA = new Set(a.map(name))
  const namesB = new Set(b.map(name))
  return {
    added: b.filter((f) => !namesA.has(name(f))),
    removed: a.filter((f) => !namesB.has(name(f))),
    kept: b.filter((f) => namesA.has(name(f))),
  }
}