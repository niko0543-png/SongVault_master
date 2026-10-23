import { describe, expect, it } from 'vitest'
import { describeBar, describeGlobal, targetBar, targetBarA, tuningName } from '../describe'
import type { BarChange } from '../scoreDiff'

describe('différences Guitar Pro en phrases (#30)', () => {
  it('écrit l’accordage de la corde grave à l’aiguë, en une ligne', () => {
    expect(tuningName([64, 59, 55, 50, 45, 40])).toBe('E A D G B E')
    expect(describeGlobal({ kind: 'tuning', track: 'Guitare', before: [64, 59, 55, 50, 45, 40], after: [64, 59, 55, 50, 45, 38] }))
      .toBe('Guitare : accordage E A D G B E → D A D G B E (les cordes et les cases jouées sont comparées telles quelles)')
  })

  it('tempo, pistes et capodastre', () => {
    expect(describeGlobal({ kind: 'tempo', before: 90, after: 120 })).toBe('Tempo : 90 → 120 BPM')
    expect(describeGlobal({ kind: 'trackAdded', track: 'Basse' })).toBe('Piste ajoutée : Basse')
    expect(describeGlobal({ kind: 'trackRemoved', track: 'Basse' })).toBe('Piste supprimée : Basse')
    expect(describeGlobal({ kind: 'capo', track: 'Guitare', before: 0, after: 2 })).toBe('Guitare : capodastre aucun → case 2')
  })

  it('numérote les mesures à partir de 1 et dit où placer la partition', () => {
    expect(describeBar({ kind: 'added', barB: 0 }, 5)).toBe('Mesure 1 : ajoutée')
    expect(describeBar({ kind: 'removed', barA: 2, beforeB: 2 }, 5)).toBe('Mesure 3 de A : supprimée, avant la mesure 3')
    expect(describeBar({ kind: 'removed', barA: 5, beforeB: 5 }, 5)).toBe('Mesure 6 de A : supprimée, à la fin')
    expect(targetBar({ kind: 'removed', barA: 5, beforeB: 5 }, 5)).toBe(4)
    const modified = { kind: 'modified' as const, barA: 3, barB: 4, notes: [{ bar: 4, beat: 0, note: 0 }, { bar: 4, beat: 1, note: 0 }], removedNotes: 1 }
    expect(describeBar(modified, 5)).toBe('Mesure 5 : 2 notes modifiées, 1 note retirée')
    expect(describeBar({ ...modified, notes: [], removedNotes: 0 }, 5)).toBe('Mesure 5 : modifiée dans une autre voix ou le chiffrage')
  })

  it('#37 : place l’ancienne partition sur la mesure de A, y compris pour une mesure ajoutée dans B', () => {
    // A : 0 1 2 3 4 ; B : X 0 1 3' 4 Y (X et Y ajoutées, 2 supprimée, 3 modifiée)
    const changes: BarChange[] = [
      { kind: 'added', barB: 0 },
      { kind: 'removed', barA: 2, beforeB: 3 },
      { kind: 'modified', barA: 3, barB: 3, notes: [], removedNotes: 1 },
      { kind: 'added', barB: 5 },
    ]
    expect(changes.map((change) => targetBarA(change, changes, 5))).toEqual([0, 2, 3, 4])
  })
})
