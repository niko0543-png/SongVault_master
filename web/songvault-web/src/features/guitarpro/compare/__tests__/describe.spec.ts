import { describe, expect, it } from 'vitest'
import { describeBar, describeGlobal, targetBar, tuningName } from '../describe'

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
})