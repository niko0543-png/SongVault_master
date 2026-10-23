import { describe, expect, it } from 'vitest'
import {
  guessKey, isChord, parseChord, prefersFlats, transposeChord, transposeKey,
} from '../chords'

describe('parseChord', () => {
  it('sépare la note, le suffixe et la basse', () => {
    expect(parseChord('C#m7b5/G#')).toEqual({ root: 'C#', suffix: 'm7b5', bass: 'G#' })
    expect(parseChord('Bbmaj7')).toEqual({ root: 'Bb', suffix: 'maj7', bass: null })
  })

  it('reconnaît les accords usuels', () => {
    for (const chord of ['C', 'Am', 'F#m', 'G7', 'Dsus4', 'Cadd9', 'E7(#9)', 'Bdim', 'Caug', 'C°', 'Bø', 'CΔ7', 'D/F#', 'Em7-5']) {
      expect(isChord(chord), chord).toBe(true)
    }
  })

  it('ne prend pas un titre de partie pour un accord', () => {
    for (const text of ['Refrain', 'Bridge', 'Coda', 'Couplet 1', 'Am ', 'H7', 'x2', '']) {
      expect(isChord(text), text).toBe(false)
    }
  })
})

describe('transposeChord', () => {
  it('garde le suffixe et transpose la basse', () => {
    expect(transposeChord('Am7', 3, true)).toBe('Cm7')
    expect(transposeChord('Dsus4', 2, false)).toBe('Esus4')
    expect(transposeChord('C/G', 2, false)).toBe('D/A')
    expect(transposeChord('G/B', 1, true)).toBe('Ab/C')
  })

  it('écrit en dièses ou en bémols selon le choix', () => {
    expect(transposeChord('C', 1, false)).toBe('C#')
    expect(transposeChord('C', 1, true)).toBe('Db')
    expect(transposeChord('C', -1, false)).toBe('B')
  })

  it('rend l’accord tel quel sans décalage, même s’il s’écrit autrement', () => {
    expect(transposeChord('Cb', 0, false)).toBe('Cb')
    expect(transposeChord('E#m', 12, false)).toBe('E#m')
    expect(transposeChord('Gb7', -12, false)).toBe('Gb7')
  })

  it('ne touche pas à ce qui n’est pas un accord', () => {
    expect(transposeChord('Refrain', 5, false)).toBe('Refrain')
  })
})

describe('tonalités', () => {
  it('donne la tonalité d’arrivée sous son nom usuel', () => {
    expect(transposeKey('Am', 3)).toBe('Cm')
    expect(transposeKey('C', 3)).toBe('Eb')
    expect(transposeKey('Em', 2)).toBe('F#m')
    expect(transposeKey('G', -1)).toBe('F#')
    expect(transposeKey('F#m', 12)).toBe('F#m')
    expect(transposeKey('', 3)).toBeNull()
  })

  it('devine la tonalité au premier accord quand la version n’en a pas', () => {
    expect(guessKey('', ['Am', 'F', 'C'])).toBe('Am')
    expect(guessKey(null, ['Cmaj7', 'Am'])).toBe('C')
    expect(guessKey(' F#m ', ['A'])).toBe('F#m')
    expect(guessKey(null, [])).toBeNull()
  })

  it('choisit les bémols pour une tonalité à bémols, les dièses sinon', () => {
    expect(prefersFlats('Eb', ['C#'])).toBe(true)
    expect(prefersFlats('Cm', [])).toBe(true)
    expect(prefersFlats('E', ['Bb', 'Eb'])).toBe(false)
    expect(prefersFlats('F#m', [])).toBe(false)
  })

  it('suit l’écriture de la source en do majeur ou la mineur', () => {
    expect(prefersFlats('C', ['Bb', 'F', 'C'])).toBe(true)
    expect(prefersFlats('Am', ['E/G#', 'Am'])).toBe(false)
    expect(prefersFlats(null, ['Ab', 'Eb', 'F#'])).toBe(true)
  })
})