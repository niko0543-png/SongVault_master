import { describe, expect, it } from 'vitest'
import type { model } from '@coderline/alphatab'
import { applyStaffView, fileStaves, layoutFor } from '../display'

/** #31 : réglages du rendu façon Guitar Pro (ADR 0037). */
type Staff = { isStringed: boolean; showStandardNotation: boolean; showTablature: boolean }
const scoreOf = (...staves: Staff[]) => ({ tracks: staves.map((s) => ({ staves: [s] })) }) as unknown as model.Score

describe('display (#31)', () => {
  it('suit la mise en page du fichier sur grand écran, et range les mesures lui-même sur téléphone', () => {
    expect(layoutFor(true)).toBe(2)                                 // LayoutMode.Parchment
    expect(layoutFor(false)).toBe(0)                                // LayoutMode.Page
  })

  it('applique le choix aux pistes à cordes, laisse la batterie telle quelle, et revient au fichier', () => {
    const guitar = { isStringed: true, showStandardNotation: false, showTablature: true }
    const drums = { isStringed: false, showStandardNotation: true, showTablature: false }
    const score = scoreOf(guitar, drums)
    const original = fileStaves(score)

    applyStaffView(score, 'both', original)
    expect(guitar).toMatchObject({ showStandardNotation: true, showTablature: true })
    applyStaffView(score, 'tab', original)
    expect(guitar).toMatchObject({ showStandardNotation: false, showTablature: true })
    expect(drums).toMatchObject({ showStandardNotation: true, showTablature: false })
    applyStaffView(score, 'score', original)
    expect(guitar).toMatchObject({ showStandardNotation: true, showTablature: false })
    applyStaffView(score, 'file', original)
    expect(guitar).toMatchObject({ showStandardNotation: false, showTablature: true })
  })
})
