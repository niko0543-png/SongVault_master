import { guessKey, isChord, prefersFlats, transposeChord, transposeKey } from './chords'

/**
 * #25 : paroles au format ChordPro (ADR 0032). Les accords sont entre crochets dans le texte, juste avant la
 * syllabe où ils tombent : « [Am]Je marche [F]seul ». Des paroles sans crochets restent des paroles.
 *
 * - « {c: Refrain} » ou « {comment: …} » : une indication, affichée à part ; les autres directives {…} sont ignorées ;
 * - « [Refrain] » seul sur sa ligne, qui n'est pas un accord : un titre de partie ;
 * - un crochet qui n'est pas un accord, ailleurs, reste du texte.
 */

/** Un morceau de mot : l'accord qui tombe dessus (ou rien) et son texte, espaces compris. */
export interface Piece {
  chord: string | null
  text: string
}

export type SheetLine =
  | { kind: 'lyrics'; words: Piece[][]; hasChords: boolean }
  | { kind: 'comment'; text: string }
  | { kind: 'section'; text: string }
  | { kind: 'blank' }

const BRACKET = /\[([^\]\n]*)\]/g
const DIRECTIVE = /^\s*\{\s*([a-z_]+)\s*(?::\s*(.*?))?\s*\}\s*$/i
const COMMENT_DIRECTIVES = new Set(['c', 'comment', 'ci', 'comment_italic', 'cb', 'comment_box'])

/** Les accords du texte, dans l'ordre. */
export function chordsOf(source: string): string[] {
  return [...source.matchAll(BRACKET)].map((m) => m[1]!).filter(isChord)
}

export function hasChords(source: string | null | undefined): boolean {
  return !!source && chordsOf(source).length > 0
}

export function parseSheet(source: string): SheetLine[] {
  return source.replace(/\r\n?/g, '\n').split('\n').map(parseLine)
}

function parseLine(line: string): SheetLine {
  if (line.trim() === '') return { kind: 'blank' }

  const directive = DIRECTIVE.exec(line)
  if (directive) {
    const name = directive[1]!.toLowerCase()
    return COMMENT_DIRECTIVES.has(name) && directive[2] ? { kind: 'comment', text: directive[2] } : { kind: 'blank' }
  }

  const alone = /^\s*\[([^\]\n]+)\]\s*$/.exec(line)
  if (alone && !isChord(alone[1]!)) return { kind: 'section', text: alone[1]!.trim() }

  // Morceaux de la ligne : un accord et le texte qui le suit, jusqu'à l'accord suivant
  const pieces: Piece[] = []
  let last = 0
  let chord: string | null = null
  let text = ''
  for (const match of line.matchAll(BRACKET)) {
    const inner = match[1]!
    text += line.slice(last, match.index)
    last = match.index! + match[0].length
    if (!isChord(inner)) {
      text += match[0]                                    // pas un accord : du texte
      continue
    }
    if (chord !== null || text !== '') pieces.push({ chord, text })
    chord = inner
    text = ''
  }
  text += line.slice(last)
  if (chord !== null || text !== '') pieces.push({ chord, text })

  return { kind: 'lyrics', words: toWords(pieces), hasChords: pieces.some((p) => p.chord !== null) }
}

/**
 * Regroupe les morceaux en mots : la ligne ne passe à la ligne qu'entre deux mots, jamais au milieu
 * de « mar[G]che », et un accord reste au-dessus de sa syllabe.
 */
function toWords(pieces: Piece[]): Piece[][] {
  const words: Piece[][] = []
  let word: Piece[] = []
  const close = () => {
    if (word.length) words.push(word)
    word = []
  }
  for (const piece of pieces) {
    const tokens = piece.text.match(/\S+\s*|\s+/g) ?? ['']
    tokens.forEach((token, i) => {
      const chord = i === 0 ? piece.chord : null
      if (/^\s/.test(token) && chord === null) {              // des espaces seuls : la fin du mot en cours
        if (word.length) word[word.length - 1]!.text += token
        else word.push({ chord: null, text: token })
        close()
        return
      }
      word.push({ chord, text: token })
      if (/\s$/.test(token)) close()
    })
  }
  close()
  return words
}

/**
 * Transpose les seuls accords du texte, sans toucher au reste (espaces, retours à la ligne, crochets qui ne
 * sont pas des accords). Le texte enregistré n'est jamais réécrit par l'affichage : on transpose toujours depuis
 * la source, si bien qu'un décalage de 0 ou de ±12 rend la source à l'identique.
 */
export function transposeSource(source: string, semitones: number, flats: boolean): string {
  if (((semitones % 12) + 12) % 12 === 0) return source
  return source.replace(BRACKET, (whole, inner: string) =>
    isChord(inner) ? `[${transposeChord(inner, semitones, flats)}]` : whole)
}

/** Ce qu'affiche le transpositeur : la tonalité de départ, celle d'arrivée et l'écriture choisie. */
export function transposition(source: string, key: string | null | undefined, semitones: number) {
  const chords = chordsOf(source)
  const from = guessKey(key, chords)
  const to = from ? transposeKey(from, semitones) : null
  const flats = prefersFlats(to, chords)
  return { from, to, flats, text: transposeSource(source, semitones, flats) }
}