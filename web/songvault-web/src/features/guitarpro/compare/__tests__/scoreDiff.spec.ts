import { describe, expect, it } from 'vitest'
import { importer, Settings } from '@coderline/alphatab'
import { toPlainScore } from '../plain'
import { diffScores } from '../scoreDiff'
import { diffSequence, pairChanges } from '../sequence'

/** #30 : partitions écrites en alphaTex, lues par le vrai alphaTab, puis comparées. */
const STANDARD = 'E4 B3 G3 D3 A2 E2'
const DROP_D = 'E4 B3 G3 D3 A2 D2'

function score(bars: string[], options: { tempo?: number; tuning?: string; tracks?: string } = {}) {
  const tex = `\\tempo ${options.tempo ?? 90} \\track "Guitare" \\staff {tabs} \\tuning (${options.tuning ?? STANDARD}) `
    + bars.join(' | ') + (options.tracks ?? '')
  return toPlainScore(importer.ScoreLoader.loadAlphaTex(tex, new Settings()))
}

const RIFF = ['0.6.4 2.6.4 3.6.4 0.6.4', '0.5.4 2.5.4 3.5.4 0.5.4', '0.4.4 2.4.4 3.4.4 0.4.4', '5.6.2 7.6.2']

describe('différences entre deux fichiers Guitar Pro', () => {
  it('deux partitions identiques : aucune différence', () => {
    const diff = diffScores(score(RIFF), score(RIFF))
    expect(diff.global).toEqual([])
    expect(diff.tracks).toHaveLength(1)
    expect(diff.tracks[0]!.changes).toEqual([])
  })

  it('une mesure insérée au début ne marque pas tout le morceau comme modifié', () => {
    const diff = diffScores(score(RIFF), score(['12.1.1', ...RIFF]))
    expect(diff.tracks[0]!.changes).toEqual([{ kind: 'added', barB: 0 }])
  })

  it('une note changée : la mesure est modifiée, et seule cette note est désignée', () => {
    const after = [...RIFF]
    after[2] = '0.4.4 2.4.4 5.4.4 0.4.4'
    const [change] = diffScores(score(RIFF), score(after)).tracks[0]!.changes
    expect(change).toEqual({ kind: 'modified', barA: 2, barB: 2, notes: [{ bar: 2, beat: 2, note: 0 }], removedNotes: 1 })
  })

  it('un rythme changé marque les notes du temps, même aux mêmes cases', () => {
    const after = [...RIFF]
    after[3] = '5.6.4 7.6.4 7.6.2'
    const [change] = diffScores(score(RIFF), score(after)).tracks[0]!.changes
    expect(change?.kind).toBe('modified')
    expect(change?.kind === 'modified' && change.notes.length).toBeGreaterThan(0)
  })

  it('un effet ajouté (étouffé) compte comme une note modifiée', () => {
    const after = [...RIFF]
    after[0] = '0.6.4 2.6{pm}.4 3.6.4 0.6.4'
    const [change] = diffScores(score(RIFF), score(after)).tracks[0]!.changes
    expect(change).toMatchObject({ kind: 'modified', barB: 0, notes: [{ bar: 0, beat: 1, note: 0 }] })
  })

  it('une mesure supprimée est placée devant la mesure de B qui la suit', () => {
    const after = [RIFF[0]!, RIFF[2]!, RIFF[3]!]
    expect(diffScores(score(RIFF), score(after)).tracks[0]!.changes).toEqual([{ kind: 'removed', barA: 1, beforeB: 1 }])
  })

  it('un changement d’accordage est annoncé en une ligne, sans colorer les notes', () => {
    const diff = diffScores(score(RIFF), score(RIFF, { tuning: DROP_D }))
    expect(diff.global).toEqual([{ kind: 'tuning', track: 'Guitare', before: [64, 59, 55, 50, 45, 40], after: [64, 59, 55, 50, 45, 38] }])
    expect(diff.tracks[0]!.changes).toEqual([])
  })

  it('tempo changé, piste ajoutée ou supprimée : changements d’ensemble', () => {
    const bass = ' \\track "Basse" \\instrument 33 \\staff {tabs} \\tuning (G2 D2 A1 E1) 0.4.1 | 0.4.1 | 0.4.1 | 0.4.1'
    const diff = diffScores(score(RIFF, { tracks: bass }), score(RIFF, { tempo: 120 }))
    expect(diff.global).toEqual([{ kind: 'tempo', before: 90, after: 120 }, { kind: 'trackRemoved', track: 'Basse' }])
    expect(diffScores(score(RIFF), score(RIFF, { tracks: bass })).global).toEqual([{ kind: 'trackAdded', track: 'Basse' }])
  })

  it('associe les pistes par nom et instrument, même dans un autre ordre ou renommées', () => {
    const bass = (name: string) => ` \\track "${name}" \\instrument 33 \\staff {tabs} \\tuning (G2 D2 A1 E1) 0.4.1 | 0.4.1 | 0.4.1 | 3.4.1`
    const a = score(RIFF, { tracks: bass('Basse') })
    // B : la basse passe en premier, renommée, avec sa dernière note changée
    const tex = `\\tempo 90${bass('Basse électrique').replace(' | 3.4.1', ' | 5.4.1')} \\track "Guitare" \\staff {tabs} \\tuning (${STANDARD}) ${RIFF.join(' | ')}`
    const b = toPlainScore(importer.ScoreLoader.loadAlphaTex(tex, new Settings()))
    const diff = diffScores(a, b)
    expect(diff.global).toEqual([])
    expect(diff.tracks.map((t) => [t.name, t.trackA, t.trackB, t.changes.length])).toEqual([['Basse électrique', 1, 0, 1], ['Guitare', 0, 1, 0]])
  })
})

describe('différence de suites', () => {
  it('garde l’ordre et associe les changements d’un même bloc', () => {
    expect(pairChanges(diffSequence(['a', 'b', 'c'], ['a', 'x', 'c', 'd']))).toEqual([
      { kind: 'modified', a: 1, b: 1 },
      { kind: 'added', b: 3 },
    ])
  })

  it('au-delà de la limite, tout le milieu est remplacé plutôt que de bloquer la page', () => {
    const a = Array.from({ length: 2100 }, (_, i) => `a${i}`)
    const b = Array.from({ length: 2100 }, (_, i) => `b${i}`)
    const steps = diffSequence(a, b)
    expect(steps.filter((s) => s.kind === 'removed')).toHaveLength(2100)
    expect(steps.filter((s) => s.kind === 'added')).toHaveLength(2100)
  })
})