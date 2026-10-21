import { describe, expect, it } from 'vitest'
import { chordsOf, hasChords, parseSheet, transposeSource, transposition, type SheetLine } from '../chordpro'

const SONG = [
  '{title: Sous la pluie}',
  '{c: Couplet 1}',
  '[G]Je marche [D/F#]seul sous la [Em7]pluie',
  'Et la [Cadd9]ville se [D]tait',
  '',
  '[Refrain]',
  '[G]Oh, [F#m]je re[Bm]viens [A7sus4] [A7]',
].join('\n')

const words = (line: SheetLine) =>
  line.kind === 'lyrics' ? line.words.map((w) => w.map((p) => (p.chord ? `[${p.chord}]` : '') + p.text).join('')) : []

describe('parseSheet', () => {
  it('lit des paroles sans accords comme des paroles', () => {
    const [line] = parseSheet('La nuit tombe')
    expect(line).toMatchObject({ kind: 'lyrics', hasChords: false })
    expect(words(line!)).toEqual(['La ', 'nuit ', 'tombe'])
    expect(hasChords('La nuit tombe\nRefrain')).toBe(false)
  })

  it('place chaque accord sur sa syllabe et garde les mots entiers', () => {
    const line = parseSheet('Je mar[G]che [Am]seul')[0]!
    expect(line.kind === 'lyrics' && line.words[1]).toEqual([{ chord: null, text: 'mar' }, { chord: 'G', text: 'che ' }])
    expect(words(line)).toEqual(['Je ', 'mar[G]che ', '[Am]seul'])
  })

  it('distingue les indications, les titres de partie et les lignes vides', () => {
    const kinds = parseSheet(SONG).map((l) => l.kind)
    expect(kinds).toEqual(['blank', 'comment', 'lyrics', 'lyrics', 'blank', 'section', 'lyrics'])
    expect(parseSheet(SONG)[1]).toEqual({ kind: 'comment', text: 'Couplet 1' })
    expect(parseSheet(SONG)[5]).toEqual({ kind: 'section', text: 'Refrain' })
  })

  it('laisse en texte un crochet qui n’est pas un accord, au milieu d’une ligne', () => {
    expect(words(parseSheet('Refrain [x2] [G]fin')[0]!)).toEqual(['Refrain ', '[x2] ', '[G]fin'])
  })

  it('accepte les fins de ligne Windows', () => {
    expect(parseSheet('[C]Un\r\n[G]Deux').map(words)).toEqual([['[C]Un'], ['[G]Deux']])
  })

  it('liste les accords dans l’ordre, sans les titres de partie', () => {
    expect(chordsOf(SONG)).toEqual(['G', 'D/F#', 'Em7', 'Cadd9', 'D', 'G', 'F#m', 'Bm', 'A7sus4', 'A7'])
  })
})

describe('transposeSource', () => {
  it('transposer de +12 redonne l’original', () => {
    expect(transposeSource(SONG, 12, false)).toBe(SONG)
    expect(transposeSource(SONG, -12, true)).toBe(SONG)
  })

  it('aller-retour +3 puis −3 identique', () => {
    const up = transposition(SONG, 'G', 3)
    expect(up.to).toBe('Bb')
    expect(transposition(up.text, up.to, -3).text).toBe(SONG)
  })

  it('aller-retour sur chaque décalage, dans une tonalité à dièses', () => {
    for (let n = 1; n < 12; n++) {
      const there = transposition(SONG, 'G', n)
      expect(transposition(there.text, there.to, -n).text, `+${n}`).toBe(SONG)
    }
  })

  it('ne réécrit que les accords', () => {
    const text = transposeSource(SONG, 2, false)
    expect(text.replace(/\[[^\]]*\]/g, '[]')).toBe(SONG.replace(/\[[^\]]*\]/g, '[]'))
    expect(text).toContain('[Refrain]')
    expect(text).toContain('[A]Je marche [E/G#]seul sous la [F#m7]pluie')
    expect(text).toContain('[B7sus4] [B7]')
  })

  it('écrit les accords dans l’écriture de la tonalité d’arrivée', () => {
    expect(transposition('[Am]Je [E7]pars', 'Am', 3)).toMatchObject({ from: 'Am', to: 'Cm', flats: true, text: '[Cm]Je [G7]pars' })
    expect(transposition('[Am]Je [E7]pars', 'Am', 1)).toMatchObject({ to: 'Bbm', text: '[Bbm]Je [F7]pars' })
    expect(transposition('[Am]Je [E7]pars', 'Am', -2)).toMatchObject({ to: 'Gm', text: '[Gm]Je [D7]pars' })
    expect(transposition('[Am]Je [E7]pars', '', 4)).toMatchObject({ from: 'Am', to: 'C#m', flats: false, text: '[C#m]Je [G#7]pars' })
  })
})