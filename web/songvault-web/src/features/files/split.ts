/** Découpage d'un enregistrement en morceaux (ADR 0050) : les passages entre deux coupures, sans dépendance. */

/** Mêmes limites que l'API (AudioSplit). */
export const SPLIT_MAX_PARTS = 30
export const SPLIT_MIN_PART_MS = 1000
export const SPLIT_TITLE_MAX = 200

export interface SplitSegment {
  startMs: number
  endMs: number
  /** Décoché : le passage est laissé de côté (accordage, bavardages). */
  keep: boolean
  title: string
  /** Morceau existant qui reçoit une nouvelle version ; null : un nouveau morceau nommé title. */
  songId: string | null
}

/** Au départ, un seul passage : tout le fichier. */
export function wholeFile(durationMs: number, title = ''): SplitSegment[] {
  return [{ startMs: 0, endMs: durationMs, keep: true, title, songId: null }]
}

/**
 * Coupe le passage qui contient `atMs` en deux. Le premier garde ses réglages, le second est un nouveau passage gardé.
 * Rien ne change si la coupure laisserait un passage de moins d'une seconde.
 */
export function cutAt(segments: SplitSegment[], atMs: number): SplitSegment[] {
  const at = Math.round(atMs)
  const index = segments.findIndex((s) => at > s.startMs && at < s.endMs)
  if (index < 0) return segments
  const segment = segments[index]!
  if (at - segment.startMs < SPLIT_MIN_PART_MS || segment.endMs - at < SPLIT_MIN_PART_MS) return segments
  return [
    ...segments.slice(0, index),
    { ...segment, endMs: at },
    { startMs: at, endMs: segment.endMs, keep: true, title: '', songId: null },
    ...segments.slice(index + 1),
  ]
}

/** Retire la coupure entre le passage `index` et le suivant ; le passage fusionné garde les réglages du premier. */
export function mergeWithNext(segments: SplitSegment[], index: number): SplitSegment[] {
  const first = segments[index]
  const second = segments[index + 1]
  if (!first || !second) return segments
  return [...segments.slice(0, index), { ...first, endMs: second.endMs }, ...segments.slice(index + 2)]
}

/** Déplace la coupure entre le passage `index` et le suivant, sans laisser de passage de moins d'une seconde. */
export function moveCut(segments: SplitSegment[], index: number, toMs: number): SplitSegment[] {
  const first = segments[index]
  const second = segments[index + 1]
  if (!first || !second) return segments
  const at = Math.round(Math.min(Math.max(toMs, first.startMs + SPLIT_MIN_PART_MS), second.endMs - SPLIT_MIN_PART_MS))
  if (at <= first.startMs || at >= second.endMs) return segments
  return [
    ...segments.slice(0, index),
    { ...first, endMs: at },
    { ...second, startMs: at },
    ...segments.slice(index + 2),
  ]
}

export const keptSegments = (segments: SplitSegment[]) => segments.filter((s) => s.keep)

/** Ce qui manque pour envoyer : null si tout va bien, sinon la clé du message. */
export function splitProblem(segments: SplitSegment[]): 'none' | 'tooMany' | 'title' | null {
  const kept = keptSegments(segments)
  if (kept.length === 0) return 'none'
  if (kept.length > SPLIT_MAX_PARTS) return 'tooMany'
  if (kept.some((s) => s.title.trim() === '' || s.title.trim().length > SPLIT_TITLE_MAX)) return 'title'
  return null
}

/** Le corps de POST …/splits. */
export function toSplitRequest(segments: SplitSegment[]) {
  return {
    parts: keptSegments(segments).map((s) => ({
      startMs: s.startMs,
      endMs: s.endMs,
      title: s.title.trim(),
      ...(s.songId ? { songId: s.songId } : {}),
    })),
  }
}

/** 1:02:03 ou 4:05, au dixième de seconde près : on coupe entre deux notes. */
export function formatCutTime(ms: number): string {
  const tenths = Math.max(0, Math.round(ms / 100))
  const seconds = Math.floor(tenths / 10)
  const h = Math.floor(seconds / 3600)
  const m = Math.floor((seconds % 3600) / 60)
  const s = String(seconds % 60).padStart(2, '0')
  const rest = `${s}.${tenths % 10}`
  return h > 0 ? `${h}:${String(m).padStart(2, '0')}:${rest}` : `${m}:${rest}`
}
