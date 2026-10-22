/**
 * #30 : différence de deux suites d'empreintes (mesures, puis temps d'une mesure), comme les lignes de #9 :
 * début et fin communs retirés, puis plus longue sous-suite commune. Une mesure insérée au début
 * décale toutes les suivantes sans les marquer : seules les empreintes sans partenaire changent.
 */
export type Step =
  | { kind: 'same'; a: number; b: number }
  | { kind: 'removed'; a: number }
  | { kind: 'added'; b: number }

/** Au-delà, on ne cherche plus la sous-suite commune (mémoire en éléments A × éléments B). */
export const MAX_SEQUENCE_CELLS = 4_000_000

export function diffSequence(a: readonly string[], b: readonly string[]): Step[] {
  let start = 0
  while (start < a.length && start < b.length && a[start] === b[start]) start++
  let endA = a.length
  let endB = b.length
  while (endA > start && endB > start && a[endA - 1] === b[endB - 1]) {
    endA--
    endB--
  }

  const steps: Step[] = []
  for (let i = 0; i < start; i++) steps.push({ kind: 'same', a: i, b: i })
  middle(a, b, start, endA, start, endB, steps)
  for (let i = 0; endA + i < a.length; i++) steps.push({ kind: 'same', a: endA + i, b: endB + i })
  return steps
}

function middle(a: readonly string[], b: readonly string[], fromA: number, toA: number, fromB: number, toB: number, steps: Step[]) {
  const n = toA - fromA
  const m = toB - fromB
  if (n === 0 || m === 0 || n * m > MAX_SEQUENCE_CELLS) {
    for (let i = fromA; i < toA; i++) steps.push({ kind: 'removed', a: i })
    for (let j = fromB; j < toB; j++) steps.push({ kind: 'added', b: j })
    return
  }

  // lcs[i][j] = longueur de la plus longue sous-suite commune de a[fromA + i..] et b[fromB + j..]
  const width = m + 1
  const lcs = new Uint32Array((n + 1) * width)
  for (let i = n - 1; i >= 0; i--) {
    for (let j = m - 1; j >= 0; j--) {
      lcs[i * width + j] = a[fromA + i] === b[fromB + j]
        ? lcs[(i + 1) * width + j + 1]! + 1
        : Math.max(lcs[(i + 1) * width + j]!, lcs[i * width + j + 1]!)
    }
  }

  let i = 0
  let j = 0
  while (i < n && j < m) {
    if (a[fromA + i] === b[fromB + j]) {
      steps.push({ kind: 'same', a: fromA + i++, b: fromB + j++ })
    } else if (lcs[(i + 1) * width + j]! >= lcs[i * width + j + 1]!) {
      steps.push({ kind: 'removed', a: fromA + i++ })
    } else {
      steps.push({ kind: 'added', b: fromB + j++ })
    }
  }
  for (; i < n; i++) steps.push({ kind: 'removed', a: fromA + i })
  for (; j < m; j++) steps.push({ kind: 'added', b: fromB + j })
}

/**
 * Regroupe les pas en blocs : chaque bloc de changements donne des paires (A et B modifiés l'un en l'autre),
 * puis le reste en suppressions ou ajouts. `before` est l'index B devant lequel une suppression se place.
 */
export type Change =
  | { kind: 'modified'; a: number; b: number }
  | { kind: 'removed'; a: number; before: number }
  | { kind: 'added'; b: number }

export function pairChanges(steps: Step[]): Change[] {
  const changes: Change[] = []
  let removed: number[] = []
  let added: number[] = []
  let nextB = 0
  const flush = () => {
    const pairs = Math.min(removed.length, added.length)
    for (let k = 0; k < pairs; k++) changes.push({ kind: 'modified', a: removed[k]!, b: added[k]! })
    const firstB = added[0] ?? nextB
    for (const a of removed.slice(pairs)) changes.push({ kind: 'removed', a, before: firstB + pairs })
    for (const b of added.slice(pairs)) changes.push({ kind: 'added', b })
    removed = []
    added = []
  }
  for (const step of steps) {
    if (step.kind === 'same') {
      flush()
      nextB = step.b + 1
    } else if (step.kind === 'removed') {
      removed.push(step.a)
    } else {
      added.push(step.b)
    }
  }
  flush()
  return changes
}