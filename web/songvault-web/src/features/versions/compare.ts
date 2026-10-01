import type { SongFile } from '@/features/files/types'

/** "?a=3" → 3 ; valeur absente, invalide, nulle ou négative → null. */
export function parseVersionNumber(value: unknown): number | null {
  const raw = Array.isArray(value) ? value[0] : value
  const n = Number(raw)
  return Number.isInteger(n) && n > 0 ? n : null
}

/** Par défaut, on compare les deux versions les plus récentes : [avant-dernière, dernière]. */
export function defaultPair(numbers: number[]): [number, number] | null {
  if (numbers.length < 2) return null
  const sorted = [...numbers].sort((x, y) => x - y)
  return [sorted[sorted.length - 2]!, sorted[sorted.length - 1]!]
}

/** Premier fichier audio d'une version (celui que l'on écoute en A/B). */
export function firstAudio(files: SongFile[]): SongFile | null {
  return files.find((f) => f.fileType === 'Audio') ?? null
}
