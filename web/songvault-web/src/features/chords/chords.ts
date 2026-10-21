/**
 * #25 : accords et transposition (ADR 0032). Même grammaire que ChordPro.cs côté C#.
 *
 * Un accord : une note (A à G, # ou b), un suffixe fait des seuls mots d'accord (m, maj7, sus4, add9, dim, 7(#9)…)
 * et une basse facultative (C/G). « [Refrain] » ou « [Bridge] » ne sont donc pas des accords : leur suite
 * (« efrain », « ridge ») n'est pas un suffixe.
 */
export const CHORD_PATTERN = /^([A-G](?:#|b)?)((?:maj|min|dim|aug|sus|add|m|M|°|ø|Δ|\+|-|\d|#|b|\(|\)|,)*)(?:\/([A-G](?:#|b)?))?$/

const SHARPS = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'] as const
const FLATS = ['C', 'Db', 'D', 'Eb', 'E', 'F', 'Gb', 'G', 'Ab', 'A', 'Bb', 'B'] as const
const NATURALS: Record<string, number> = { C: 0, D: 2, E: 4, F: 5, G: 7, A: 9, B: 11 }

/** Nom usuel de chaque tonalité : on écrit Eb plutôt que D#, F#m plutôt que Gbm. */
const MAJOR_KEYS = ['C', 'Db', 'D', 'Eb', 'E', 'F', 'F#', 'G', 'Ab', 'A', 'Bb', 'B'] as const
const MINOR_KEYS = ['Cm', 'C#m', 'Dm', 'Ebm', 'Em', 'Fm', 'F#m', 'Gm', 'G#m', 'Am', 'Bbm', 'Bm'] as const
/** Tonalités qui s'écrivent avec des bémols à la clé. */
const FLAT_KEYS = new Set(['F', 'Bb', 'Eb', 'Ab', 'Db', 'Gb', 'Dm', 'Gm', 'Cm', 'Fm', 'Bbm', 'Ebm'])
/** Tonalités sans altération à la clé : l'écriture de la source décide. */
const NEUTRAL_KEYS = new Set(['C', 'Am'])

export interface Chord {
  root: string
  suffix: string
  bass: string | null
}

export function parseChord(text: string): Chord | null {
  const match = CHORD_PATTERN.exec(text)
  if (!match) return null
  return { root: match[1]!, suffix: match[2]!, bass: match[3] ?? null }
}

export function isChord(text: string): boolean {
  return CHORD_PATTERN.test(text)
}

/** Demi-tons depuis do (0 à 11) ; Cb vaut 11, E# vaut 5. */
export function noteIndex(note: string): number {
  const base = NATURALS[note[0]!]!
  const shift = note[1] === '#' ? 1 : note[1] === 'b' ? -1 : 0
  return (base + shift + 12) % 12
}

function mod12(n: number): number {
  return ((n % 12) + 12) % 12
}

export function transposeNote(note: string, semitones: number, flats: boolean): string {
  return (flats ? FLATS : SHARPS)[mod12(noteIndex(note) + semitones)]!
}

/**
 * Transpose un accord ; le suffixe ne change jamais (m7, sus4, add9). Sans décalage (0, ±12…), ou si le texte
 * n'est pas un accord, il est rendu tel quel : rien ne se perd.
 */
export function transposeChord(text: string, semitones: number, flats: boolean): string {
  const chord = parseChord(text)
  if (!chord || mod12(semitones) === 0) return text
  const root = transposeNote(chord.root, semitones, flats)
  return chord.bass ? `${root}${chord.suffix}/${transposeNote(chord.bass, semitones, flats)}` : root + chord.suffix
}

/** Une tonalité au format de la version (C, F#m, Bb) ; null si le texte n'en est pas une. */
export function parseKey(key: string | null | undefined): { index: number; minor: boolean } | null {
  const match = /^([A-G](?:#|b)?)(m?)$/.exec(key?.trim() ?? '')
  return match ? { index: noteIndex(match[1]!), minor: match[2] === 'm' } : null
}

/** La tonalité d'arrivée, sous son nom usuel ; null sans tonalité de départ. */
export function transposeKey(key: string | null | undefined, semitones: number): string | null {
  const parsed = parseKey(key)
  if (!parsed) return null
  if (mod12(semitones) === 0) return key!.trim()
  const index = mod12(parsed.index + semitones)
  return (parsed.minor ? MINOR_KEYS : MAJOR_KEYS)[index]!
}

/** Tonalité de la version, ou à défaut celle du premier accord (Am → la mineur, G7 → sol majeur). */
export function guessKey(key: string | null | undefined, chords: readonly string[]): string | null {
  if (parseKey(key)) return key!.trim()
  for (const text of chords) {
    const chord = parseChord(text)
    if (!chord) continue
    const minor = /^m(?!aj)/.test(chord.suffix) || chord.suffix.startsWith('min')
    return `${chord.root}${minor ? 'm' : ''}`
  }
  return null
}

/**
 * Dièses ou bémols à l'arrivée : ceux de la tonalité d'arrivée (Eb s'écrit avec des bémols, E avec des dièses).
 * Pour do majeur ou la mineur, sans altération à la clé, on garde l'habitude de la source.
 */
export function prefersFlats(targetKey: string | null, chords: readonly string[]): boolean {
  if (targetKey && FLAT_KEYS.has(targetKey)) return true
  if (targetKey && !NEUTRAL_KEYS.has(targetKey)) return false
  let flats = 0
  let sharps = 0
  for (const text of chords) {
    const chord = parseChord(text)
    for (const note of chord ? [chord.root, chord.bass] : []) {
      if (note?.[1] === 'b') flats++
      else if (note?.[1] === '#') sharps++
    }
  }
  return flats > sharps
}